const fs = require('fs')
const net = require('net')

const maxRollJson = JSON.parse(`
{"items":{"head":{"itemType":0,"subType":61,"affixes":[{"id":331,"tier":5,"roll":1},{"id":1,"tier":5,"roll":1},{"id":31,"tier":7,"roll":1},{"id":504,"tier":5,"roll":1}],"implicits":[1,1]}}}
`)

const payload = {
  amount: 1000,
  corruption: 0,
  dropImprint: true,
  dropMatches: false,
  forgingPotential: 35,
  item: Object.entries(maxRollJson.items)[0][1],
  ilvl: 100,
  query: fs.readFileSync('query.txt', { encoding: 'utf8' })
}

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
    if (len > this.rem()) {
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
      const current = this.ensures.shift()
      if (this.rem() >= current.len) {
        current.ensureResolve(this.read(current.len))
      }
    }
  }

  async readInt32 () {
    const buf = await this.ensure(4)
    return buf.readInt32LE(0)
  }
}

async function Main () {
  const socket = new net.Socket()
  socket.connect(5011)
  socket.on('ready', async () => {
    const jsonBuf = Buffer.from(JSON.stringify(payload))
    const buf = Buffer.alloc(6)
    const stream = new BufferStream()
    buf.writeInt32LE(jsonBuf.length + 2, 0)
    buf.writeInt16LE(1, 4)
    socket.write(Buffer.concat([buf, jsonBuf]), err => {
      if (err) {
        console.error(err)
        process.exit(1)
      }
    })
    socket.on('data', buf => {
      stream.add(buf)
    })
    socket.on('close', () => {
      process.exit(0)
    })
    const len = await stream.readInt32()
    console.log('len', len)
    if (len !== 8) {
      process.exit(0)
    }
    const passes = await stream.readInt32()
    const amount = await stream.readInt32()
    console.log('passes', passes)
    console.log('p', passes / amount)
    socket.end()
  })
}

Main()
