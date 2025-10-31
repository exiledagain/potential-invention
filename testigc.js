const fs = require('fs')
const GenerationRequest = require('./ctrl-server/common/GenerationRequest')

const maxRollJson = JSON.parse(`
{"items":{"finger2":{"itemType":21,"subType":3}}}
`)

const payload = {
  amount: 21,
  corruption: 0,
  dropImprint: true,
  dropMatches: false,
  forgingPotential: 35,
  item: Object.entries(maxRollJson.items)[0][1],
  ilvl: 100,
  faction: 0,
  query: fs.readFileSync('query.txt', { encoding: 'utf8' })
}

async function Main () {
  const ret = await GenerationRequest(payload)
  if (typeof ret === 'string') {
    console.error(ret)
    return
  }
  console.log('passes', ret.passes)
  console.log('amount', ret.amount)
  console.log('p', ret.passes / ret.amount)
}

Main().then(process.exit).catch(process.exit)
