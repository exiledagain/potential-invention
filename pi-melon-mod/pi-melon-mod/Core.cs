using MelonLoader;
using pi_melon_mod.Server;
using pi_melon_mod.RemoteCommands;
using pi_melon_mod.RemoteCommands.Commands;
using System.Collections.Concurrent;
using Il2Cpp;

[assembly: MelonInfo(typeof(pi_melon_mod.Core), "pi-melon-mod", "1.0.0", "potential-invention", null)]
[assembly: MelonGame("Eleventh Hour Games", "Last Epoch")]

namespace pi_melon_mod
{
    public class Core : MelonMod
    {
        public delegate void Runnable();
        protected int Port;
        private ByteServer server;
        private Dictionary<int, RemoteCommand> commands;
        private ConcurrentQueue<IEnumerable<bool>> workQueue;

        public override void OnInitializeMelon()
        {
            workQueue = new();
            LoadPreferences(MelonPreferences.GetCategory("pi-melon-mod"));
            LoadCommands();
            server = new ByteServer(Port);
            server.Start();
            server.OnRemoteRequest += ByteServer_OnRemoteRequest;
            LoggerInstance.Msg("Initialized.");
        }

        public override void OnSceneWasLoaded(int buildIndex, string sceneName)
        {
            LoggerInstance.Msg("OnSceneWasLoaded buildIndex={0} sceneName={1}", buildIndex, sceneName);
            base.OnSceneWasLoaded(buildIndex, sceneName);
        }

        public override void OnSceneWasUnloaded(int buildIndex, string sceneName)
        {
            LoggerInstance.Msg("OnSceneWasUnloaded buildIndex={0} sceneName={1}", buildIndex, sceneName);
            base.OnSceneWasUnloaded(buildIndex, sceneName);
        }

        private void ByteServer_OnRemoteRequest(byte[] data, ByteServer.ServerResponse response)
        {
            using var reader = new BinaryReader(new MemoryStream(data));
            var type = reader.ReadInt16();
            if (data.Length >= 2 && commands.TryGetValue(type, out var command))
            {
                byte[] buf = new byte[data.Length - 2];
                Array.Copy(data, 2, buf, 0, buf.Length);
                response.Write(command.Receive(this, buf));
            }
        }

        private void LoadCommands()
        {
            commands = [];
            var igc = new ImprintGeneratorCommand();
            commands.Add(igc.TypeId, igc);
        }

        private void LoadPreferences(MelonPreferences_Category category)
        {
            if (!int.TryParse(category?.GetEntry("port")?.GetValueAsString(), out Port))
            {
                Port = 5011;
            }
        }

        public void EnqueueWork(IEnumerable<bool> r)
        {
            workQueue.Enqueue(r);
        }

        public override void OnUpdate()
        {
            while (!workQueue.IsEmpty)
            {
                if (workQueue.TryPeek(out var result))
                {
                    var enumerator = result.GetEnumerator();
                    if (enumerator.MoveNext())
                    {
                        if (!enumerator.Current)
                        {
                            workQueue.TryDequeue(out var _);
                        }
                    }
                    else
                    {
                        workQueue.TryDequeue(out var _);
                    }
                }
            }
            base.OnUpdate();
        }

        public virtual bool Ready()
        {
            return PlayerFinder.getPlayerActor().IsNotNullOrDestroyed();
        }
    }
}