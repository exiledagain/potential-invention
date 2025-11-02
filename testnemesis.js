const fs = require('fs')
const NemesisRequest = require('./ctrl-server/common/NemesisRequest')

const maxRollJson = JSON.parse(`
{"items":{"weapon":{"itemType":12,"subType":10,"uniqueID":365}}}
`)

const payload = {
  amount: 1e5,
  dropEgg: false,
  dropMatches: true,
  dropOriginalOnMatch: true,
  empowers: 2,
  faction: 0,
  ilvl: 100,
  item: Object.entries(maxRollJson.items)[0][1],
  query: fs.readFileSync('query.txt', { encoding: 'utf8' }),
  rarity: 515 / 100,
  useActive: false,
  void: true
}

async function Main () {
  const ret = await NemesisRequest(payload)
  if (typeof ret === 'string') {
    console.error(ret)
    return
  }
  console.log('passes', ret.passes)
  console.log('amount', ret.amount)
  console.log('p', ret.passes / ret.amount)
}

Main()
