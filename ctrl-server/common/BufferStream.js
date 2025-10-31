class BufferStream {
  constructor () {
    this.bufs = []
    this.ensures = []
    this.length = 0
    this.offset = 0
  }

  read (len) {
    if (len > this.rem()) {
      throw new Error('do not call read when len > rem')
    }
    let rem = len
    const bufs = []
    while (rem > 0) {
      const canRead = this.bufs[0].length - this.offset
      const toBeRead = Math.min(canRead, rem)
      rem -= toBeRead
      bufs.push(this.bufs[0].subarray(this.offset, this.offset + toBeRead))
      this.offset += toBeRead
      if (this.offset >= this.bufs[0].length) {
        this.offset = 0
        this.bufs.shift()
      }
    }
    this.length -= len
    const res = Buffer.concat(bufs)
    return res
  }

  ensure (len) {
    if (len > this.rem() || this.ensures.length > 0) {
      const res = { len }
      const ensurePromise = new Promise((resolve, reject) => {
        res.ensureResolve = resolve
        res.ensureReject = reject
      })
      res.ensurePromise = ensurePromise
      this.ensures.push(res)
      return ensurePromise
    }
    return this.read(len)
  }

  rem () {
    return this.length
  }

  async add (buf) {
    this.bufs.push(buf)
    this.length += buf.length
    while (this.ensures.length > 0) {
      const current = this.ensures[0]
      if (this.rem() >= current.len) {
        this.ensures.shift()
        current.ensureResolve(this.read(current.len))
        continue
      }
      break
    }
  }

  async readInt32 () {
    const buf = await this.ensure(4)
    return buf.readInt32LE(0)
  }
}

module.exports = BufferStream
