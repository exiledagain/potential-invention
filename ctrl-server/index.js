const dotenv = require('dotenv')
const express = require('express')
const fs = require('fs')
const https = require('https')

const GenerationRequest = require('./common/GenerationRequest')
const NemesisRequest = require('./common/NemesisRequest')
const RandomDropRequest = require('./common/RandomDropRequest')
const Lock = require('./common/Lock')

dotenv.config({ quiet: true })

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

const AllowCors = (_, res) => {
  res.setHeader('Access-Control-Allow-Headers', 'Content-Type')
  res.setHeader('Access-Control-Allow-Origin', '*')
  res.setHeader('Access-Control-Allow-Credentials', 'false')
  res.setHeader('Allow', 'OPTIONS, POST')
  res.setHeader('Vary', 'Origin')
  res.end()
}

app.options('/randomdrop', AllowCors)
app.options('/generate', AllowCors)
app.options('/nemesis', AllowCors)

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
      amount: clamp(Number(payload.amount), 1, 10000),
      corruption: clamp(Number(payload.corruption), 0, 100000),
      faction: clamp(Number(payload.faction), 0, 1),
      forgingPotential: clamp(Number(payload.forgingPotential), 0, 60),
      legendaryPotential: clamp(Number(payload.legendaryPotential), 0, 28),
      item: firstItemSlot[1],
      query: payload.filter,
      dropImprint: false,
      dropMatches: false,
      ilvl: 100
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

app.post('/nemesis', async (req, res) => {
  console.log(`/nemesis at ${new Date().toLocaleString()}`)
  res.setHeader('Access-Control-Allow-Origin', '*')
  const payload = req.body
  let firstItemSlot
  let data
  try {
    firstItemSlot = payload.equipment.length > 0 ? Object.entries(JSON.parse(payload.equipment).items)[0] : null
    data = {
      amount: clamp(Number(payload.amount), 1, 10000),
      dropEgg: false,
      dropMatches: false,
      dropOriginalOnMatch: false,
      empowers: clamp(Number(payload.empowers), 0, 2),
      faction: clamp(Number(payload.faction), 0, 1),
      ilvl: 100,
      item: firstItemSlot ? firstItemSlot[1] : undefined,
      query: payload.filter,
      rarity: clamp(Number(payload.rarity), 0, 1e5) / 100,
      useActive: false,
      void: payload.void > 0
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
    const ret = await NemesisRequest(data)
    res.statusCode = 200
    if (typeof ret === 'string') {
      res.end(ret)
      return
    }
    res.end(`${ret.passes}/${ret.amount} ${(data.faction ? 'MG' : 'CoF')} (${firstItemSlot ? firstItemSlot[0] : 'No Item'}).`)
  } catch (e) {
    console.error(e)
    res.statusCode = 500
    res.end('We tried and failed.')
  } finally {
    lock.release()
  }
})

app.post('/randomdrop', async (req, res) => {
  console.log(`/randomdrop at ${new Date().toLocaleString()}`)
  res.setHeader('Access-Control-Allow-Origin', '*')
  const payload = req.body
  let data
  try {
    data = {
      amount: clamp(Number(payload.amount), 1, 10000),
      dropMatches: false,
      faction: clamp(Number(payload.faction), 0, 1),
      ilvl: 100,
      query: payload.filter,
      rarity: clamp(Number(payload.rarity), 0, 5e5) / 100,
      corruption: clamp(Number(payload.corruption), 0, 100000),
      useActive: false
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
    const ret = await RandomDropRequest(data)
    res.statusCode = 200
    if (typeof ret === 'string') {
      res.end(ret)
      return
    }
    res.end(`${ret.passes}/${ret.amount} ${(data.faction ? 'MG' : 'CoF')}`)
  } catch (e) {
    console.error(e)
    res.statusCode = 500
    res.end('We tried and failed.')
  } finally {
    lock.release()
  }
})
