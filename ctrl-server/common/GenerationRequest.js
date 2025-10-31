const net = require('net')
const BufferStream = require('./BufferStream')

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
          socket.end()
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

module.exports = GenerationRequest
