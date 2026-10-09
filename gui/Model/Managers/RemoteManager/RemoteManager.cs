using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using gui.Model.Managers.PlayerManager;
using gui.Model.Persistence;
using Serilog;

namespace gui.Model.Managers.RemoteManager
{
    public enum Button
    {
        BtnA = 1,
        BtnB = 2,
        BtnC = 4,
        BtnD = 8,
    }

    public class RemoteManager
    {
        private static readonly RemoteManager _instance = new();
        public static RemoteManager Instance => _instance;
        private RemoteManager() { }

        private readonly Dictionary<int, Player> _remotes = [];

        private readonly Dictionary<string, int> _playersRemote = [];

        public event Action<Player, Button>? ButtonPressed;

        public void Clear()
        {
            _remotes.Clear();
            _playersRemote.Clear();
        }

        public void OnButtonPressed(int remoteId, int btn)
        {
            if (InputGate.Closed)
                return;

            Log.Information($"remoteId: {remoteId}, btn: {btn}");

            if (!_remotes.TryGetValue(remoteId, out var player) ||
                !Enum.IsDefined(typeof(Button), btn))
                return;

            App.LogPanelViewModel.Add($"Player: {_remotes[remoteId].Name} pressed {btn}");

            ButtonPressed?.Invoke(player, (Button)btn);
        }

        public void AssignRemote(int remoteId, Player player)
        {
            Log.Information($"{remoteId} was assigned to {player.Name}");

            // Remove old mapping if this remote was already assigned
            if (_remotes.TryGetValue(remoteId, out var oldPlayer))
            {
                _playersRemote.Remove(oldPlayer.Name);
            }

            // Remove old remote if the player was already assigned a different one
            if (_playersRemote.TryGetValue(player.Name, out var oldRemoteId) && oldRemoteId != remoteId)
            {
                _remotes.Remove(oldRemoteId);
            }

            _remotes[remoteId] = player;
            _playersRemote[player.Name] = remoteId;
        }



        public int GetPlayerRemote(string playerName)
        {
            if (_playersRemote.TryGetValue(playerName, out int remoteId))
                return remoteId;

            return 0;
        }
    }
}
