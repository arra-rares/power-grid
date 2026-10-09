using Serilog;
using System.Net.Sockets;

namespace gui.Model.Managers.InputManager
{
    /// <summary>
    /// Singleton class responsible for handling player input via UDP.
    /// Listens for remote control and RFID card events and dispatches them as events.
    /// </summary>
    public class InputManager
    {
        // ====== ENUMS ======

        /// <summary>
        /// Represents the type of event received via UDP.
        /// </summary>
        public enum EventType
        {
            Card = 0x01,    // RFID card scanned
            Remote = 0x02   // Remote control button pressed
        }

        // ====== SINGLETON INSTANCE ======

        private static readonly InputManager _instance = new();
        public static InputManager Instance => _instance;

        // ====== EVENTS ======

        /// <summary>
        /// Event triggered when a remote control button is pressed.
        /// </summary>
        public event Action<int, int>? BtnPressed;

        /// <summary>
        /// Event triggered when an RFID card is scanned.
        /// </summary>
        public event Action<int>? CardScanned;

        // ====== PRIVATE FIELDS ======

        private UdpClient? _udpClient;
        private CancellationTokenSource? _cts;
        private Task? _inputTask;

        // ====== CONSTRUCTOR ======

        /// <summary>
        /// Private constructor to enforce the Singleton pattern.
        /// </summary>
        private InputManager() { }

        // ====== PUBLIC METHODS ======

        /// <summary>
        /// Starts listening for input events asynchronously.
        /// </summary>
        public bool Start()
        {
            Stop();
            UdpClient client;
            try
            {
                client = new UdpClient(8081);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "UDP port 8081 is unavailable. Input stays disabled.");
                return false;
            }

            _udpClient = client;
            _cts = new CancellationTokenSource();
            var token = _cts.Token;
            _inputTask = Task.Run(() => Run(token, client));
            Log.Information($"{nameof(InputManager)}: Start");
            return true;
        }

        /// <summary>
        /// Stops listening for input events and shuts down the UDP server.
        /// </summary>
        public void Stop()
        {
            try
            {
                _cts?.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }

            try
            {
                _inputTask?.Wait(TimeSpan.FromSeconds(2));
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "InputManager stop waited on a failed listener.");
            }

            try
            {
                _udpClient?.Close();
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "InputManager socket close failed.");
            }

            _udpClient = null;
            _cts?.Dispose();
            _cts = null;
            _inputTask = null;
        }

        // ====== PRIVATE METHODS ======

        /// <summary>
        /// Runs the input listening loop, processing UDP packets.
        /// </summary>
        private async Task Run(CancellationToken cancellationToken, UdpClient client)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var result = await client.ReceiveAsync(cancellationToken);
                    HandleReceivedPacket(result.Buffer);
                }
            }
            catch (OperationCanceledException)
            {
                Log.Information("InputManager: Shutdown requested.");
            }
            catch (ObjectDisposedException)
            {
                Log.Information("InputManager: Socket closed.");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "InputManager error");
            }
            finally
            {
                try
                {
                    client.Close();
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "InputManager listener close failed.");
                }
            }
        }

        /// <summary>
        /// Processes received UDP packets and raises the appropriate event.
        /// </summary>
        private void HandleReceivedPacket(byte[] buffer)
        {
            Log.Information($"Received {buffer.Length}");

            if (buffer.Length != 8)
                return; // Ignore invalid packets

            // Extract two 4-byte integers from the buffer
            int messageType = BitConverter.ToInt32(buffer, 0);
            int payload = BitConverter.ToInt32(buffer, 4);

            Log.Information($"{messageType} {payload}");
            switch ((EventType)messageType)
            {
                case EventType.Card:
                    CardScanned?.Invoke(payload); // Card ID is in the second int
                    break;

                case EventType.Remote:
                    Log.Information($"{payload >> 4} {payload & 0x0F}");
                    BtnPressed?.Invoke(payload >> 4, payload & 0x0F); // Extract two values
                    break;
            }
        }

    }
}
