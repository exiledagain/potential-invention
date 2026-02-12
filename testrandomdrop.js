const fs = require('fs')
const RandomDropRequest = require('./ctrl-server/common/RandomDropRequest')

// 879, 972 ~ 27.7%
// 585, 625 ~ 20.5%
// 100, 85  ~  8.5%

const payload = {
  amount: 1e5,
  dropMatches: false,
  faction: 0,
  ilvl: 100,
  query: fs.readFileSync('query.txt', { encoding: 'utf8' }),
  rarity: 85 / 100,
  corruption: 100,
  useActive: false
}

async function Main () {
  const ret = await RandomDropRequest(payload)
  if (typeof ret === 'string') {
    console.error(ret)
    return
  }
  console.log('passes', ret.passes)
  console.log('amount', ret.amount)
  console.log('p', ret.passes / ret.amount)
}

Main()
