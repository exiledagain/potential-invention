using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace pi_melon_mod.RemoteCommands
{
    enum RemoteCommandId
    {
        None,
        ImprintGenerator,
        Nemesis,
        RandomDrop,
    }
    internal abstract class RemoteCommand(int typeId, string name)
    {
        public int TypeId { get; private set; } = typeId;
        public string Name { get; private set; } = name;

        public abstract byte[] Receive(Core core, byte[] data);
    }
}
