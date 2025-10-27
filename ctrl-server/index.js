const dotenv = require('dotenv')
const express = require('express')
const net = require('net')
const https = require('https')
const fs = require('fs')

dotenv.config({ quiet: true })

class Lock {
  constructor () {
    this._locked = false
    this._queue = []
  }

  async acquire () {
    if (!this._locked) {
      this._locked = true
      return
    }

    let resolver
    const promise = new Promise(resolve => {
      resolver = resolve
    })
    this._queue.push(resolver)
    return promise
  }

  release () {
    if (this._queue.length > 0) {
      const resolver = this._queue.shift()
      resolver()
    } else {
      this._locked = false
    }
  }
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

const clamp = (v, a, b) => {
  if (!Number.isFinite(v)) {
    return a
  }
  return Math.max(a, Math.min(v, b))
}

async function GenerationRequest (data) {
  data.dropImprint = false
  data.dropMatches = false
  data.ilvl = 100
  return new Promise((resolve, reject) => {
    const stream = new BufferStream()
    const socket = new net.Socket()
    socket.connect(5011)
    socket.on('error', e => {
      console.error(e)
      resolve('We tried to connect and failed.')
    })
    socket.on('close', () => {
      setTimeout(() => {
        resolve('This one is on us.')
      }, 2000)
    })
    socket.on('data', buf => {
      stream.add(buf)
    })
    socket.on('ready', async () => {
      const jsonBuf = Buffer.from(JSON.stringify(data))
      const buf = Buffer.alloc(6)
      buf.writeInt32LE(jsonBuf.length + 2, 0)
      buf.writeInt16LE(1, 4)
      socket.write(Buffer.concat([buf, jsonBuf]), err => {
        if (err) {
          reject(err)
        }
      })
      const len = await stream.readInt32()
      if (len !== 8) {
        resolve('Last Epoch is unavailable or your data is wrong.')
        socket.end()
        return
      }
      const passes = await stream.readInt32()
      const amount = await stream.readInt32()
      resolve({
        passes,
        amount
      })
      socket.end()
    })
  })
}

const app = express()
app.disable('x-powered-by')
app.use(express.json({ limit: '5mb' }))

https.createServer({
  key: fs.readFileSync(process.env.pi_cs_key),
  cert: fs.readFileSync(process.env.pi_cs_cert)
}, app).listen(443)

app.options('/generate', (req, res) => {
  res.setHeader('Access-Control-Allow-Headers', 'Content-Type')
  res.setHeader('Access-Control-Allow-Origin', '*')
  res.setHeader('Access-Control-Allow-Credentials', 'false')
  res.setHeader('Allow', 'OPTIONS, POST')
  res.setHeader('Vary', 'Origin')
  res.end()
})

const lock = new Lock()
app.post('/generate', async (req, res) => {
  console.log(`/generate at ${new Date().toLocaleString()}`)
  res.setHeader('Access-Control-Allow-Origin', '*')
  const payload = req.body
  let firstItemSlot
  let data
  try {
    firstItemSlot = Object.entries(JSON.parse(payload.equipment).items)[0]
    data = {
      amount: clamp(Number(payload.amount), 10, 10000),
      corruption: clamp(Number(payload.corruption), 0, 100000),
      faction: clamp(Number(payload.faction), 0, 1),
      forgingPotential: clamp(Number(payload.forgingPotential), 0, 60),
      item: firstItemSlot[1],
      query: payload.filter
    }
  } catch (e) {
    console.error(e)
    res.statusCode = 400
    res.end('Your data could not be parsed.')
    return
  }
  try {
    await lock.acquire()
    if (res.closed) {
      res.end('Nothing to do here.')
      return
    }
    const ret = await GenerationRequest(data)
    res.statusCode = 200
    if (typeof ret === 'string') {
      res.end(ret)
      return
    }
    res.end(`Your imprint slot (${firstItemSlot[0]}) passed ${ret.passes}/${ret.amount} ${(data.faction ? 'MG' : 'CoF')}.`)
  } catch (e) {
    console.error(e)
    res.statusCode = 500
    res.end('We tried and failed.')
  } finally {
    lock.release()
  }
})
