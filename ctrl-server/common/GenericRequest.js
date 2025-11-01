const net = require('net')
const BufferStream = require('./BufferStream')

async function GenerationRequest (id, data) {
  return new Promise((resolve, reject) => {
    let resolved = false
    const stream = new BufferStream()
    const socket = new net.Socket()
    socket.setNoDelay(true)

    const done = (error, result) => {
      if (resolved) return
      resolved = true
      if (error) {
        reject(error)
      } else {
        resolve(result)
      }
      socket.end()
    }
    socket.on('error', e => done(e))
    socket.on('close', () => {
      if (!resolved) {
        done(new Error('An attempt was made and failed.'))
      }
    })
    socket.on('data', buf => stream.add(buf))
    socket.on('ready', async () => {
      try {
        const jsonBuf = Buffer.from(JSON.stringify(data))
        const buf = Buffer.alloc(6)
        buf.writeInt32LE(jsonBuf.length + 2, 0)
        buf.writeInt16LE(id, 4)

        socket.write(Buffer.concat([buf, jsonBuf]))

        const length = await stream.readInt32()

        done(null, { length, stream })
      } catch (err) {
        done(err)
      }
    })

    socket.connect(5011)
  })
}

module.exports = GenerationRequest
