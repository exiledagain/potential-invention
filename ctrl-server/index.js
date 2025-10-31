const dotenv = require('dotenv')
const express = require('express')
const fs = require('fs')
const https = require('https')
const GenerationRequest = require('./common/GenerationRequest')

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

const clamp = (v, a, b) => {
  if (!Number.isFinite(v)) {
    return a
  }
  return Math.max(a, Math.min(v, b))
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
