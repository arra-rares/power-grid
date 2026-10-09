using gui.Model.Managers.MarketManager;
using gui.Model.Managers.PlayerManager;
using gui.Model.Managers.RemoteManager;
using Serilog;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Media;
using System.Text;
using System.Threading.Tasks;

namespace gui.Model.Phases.ResourceBuyingPhase
{
    public class ResourceBuyingPhase(Action<PurchaseData>? purchaseUpdated) : Phase
    { 
        private Action<PurchaseData>? _purchaseUpdated = purchaseUpdated;

        private BuyStep? _buyStep;

        public override async Task Execute()
        {
            Log.Information("Running: ResourceBuyingPhase");

            var players = new Stack<Player>(PlayerManager.Instance.GetPlayers());
            while (players.Count > 0)
            {
                var path = Path.Combine(AppContext.BaseDirectory, "Assets", "Sounds", "attention.wav");
                using (var media = new SoundPlayer(path))
                {
                    media.Play(); // or PlaySync() if you want to block
                }

                var player = players.Pop();
                _buyStep = new BuyStep(player);

                // hook up
                _buyStep.PurchaseUpdated += OnPurchaseUpdated;
                RemoteManager.Instance.ButtonPressed += _buyStep.HandleButtonPressed;
                _buyStep.Init();
                await _buyStep.Run();
                RemoteManager.Instance.ButtonPressed -= _buyStep.HandleButtonPressed;
                _buyStep.PurchaseUpdated -= OnPurchaseUpdated;
            }
        }

        public void OnPurchaseUpdated(PurchaseData purchaseData)
        {
            _purchaseUpdated?.Invoke(purchaseData);
        }

        public void UpdatePurchaseRecord(ResourceType type, int amount)
        {
            _buyStep?.UpdatePurchaseRecord(type, amount);
        }

        public void DonePurchase()
        {
            Log.Information("DonePurchase"); 
            _buyStep?.CompletePhase();
        }
    }
}

