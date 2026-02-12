const GenericRequest = require('./GenericRequest')

async function NemesisRequest (data) {
  const ret = await GenericRequest(3, data)
  if (typeof ret !== 'object') {
    return ret
  }
  if (ret.length !== 8) {
    return 'Last Epoch is unavailable or your data is wrong.'
  }
  const passes = await ret.stream.readInt32()
  const amount = await ret.stream.readInt32()
  return { passes, amount }
}

module.exports = NemesisRequest
