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
  faction: 0,
  forgingPotential: 35,
  ilvl: 100,
  item: Object.entries(maxRollJson.items)[0][1],
  query: fs.readFileSync('query.txt', { encoding: 'utf8' }),
  legendaryPotential: 0
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

Main()
