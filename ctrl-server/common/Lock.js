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
    return await promise
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

module.exports = Lock
