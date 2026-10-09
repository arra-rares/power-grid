using gui.Model.Managers.CardManager;
using gui.Model.Managers.InfoManager;
using gui.Model.Managers.PlayerManager;
using gui.Model.Managers.RemoteManager;
using Serilog;
using static gui.Model.Managers.PlayerManager.Status;

namespace gui.Model.Phases.AuctionPhase
{
    public class ThrowCardStep(AuctionContext ctx) : Step(ctx)
    {
        public override async Task<Step?> Execute()
        {
            if (_ctx.Card == null)
            {
                Log.Warning("Card null or no participants");
                return new StartAuctionStep(_ctx);
            }

            if (_ctx.Participants.Count == 0)
            {
                return new StartAuctionStep(_ctx);
            }

            var player = _ctx.Participants.First();
            Log.Information("{PlayerName} to act.", player.Name);

            CardManager.Instance.AssignCard(player, _ctx.Card);

            if (_ctx.Card.Type == CardType.Utility)
            {
                HandleUtilityCard();
                Log.Information("return startAuctionStep");
                return new StartAuctionStep(_ctx);
            }

            if (player.CountPowerPlantCards() == 1)
            {
                PlayerManager.Instance.SetPlayerState(_ctx.Participants.First(), PlayerState.Done);
                _ctx.DonePlayers.Add(_ctx.Participants.First());
                return new StartAuctionStep(_ctx);
            }

            PlayerManager.Instance.SetPlayerState(player, PlayerState.Active);
            UpdateInfo();

            return await _stepCompletion.Task;
        }


        public override void HandleButtonPressed(Player player, Button btn)
        {
            if (player is not { Status.State: PlayerState.Active })
                return;

            if (btn == Button.BtnB)
            {
                PlayerManager.Instance.SetPlayerState(player, PlayerState.Done);
                _ctx.DonePlayers.Add(player);
                _stepCompletion.TrySetResult(new StartAuctionStep(_ctx));
            }
        }

        public override void HandleNewCardScanned(Card card)
        {
            if (_ctx.Card != null && _ctx.Card.Type == CardType.PowerPlant)
            {
                PlayerManager.Instance.SetPlayerState(_ctx.Participants.First(), PlayerState.Done);
                _ctx.DonePlayers.Add(_ctx.Participants.First());
                _stepCompletion.TrySetResult(new StartAuctionStep(_ctx));
            }
        }

        private void HandleUtilityCard()
        {
            Log.Information("HandleUtilityCard");
            if (_ctx.Card != null && _ctx.Card.Bureaucrat)
                PlayerManager.Instance.ApplyBureaucrat();
        }

        public void UpdateInfo()
        {
            InfoManager.Instance.PhaseName = "Auction: Throw Card";
            InfoManager.Instance.PlayerName = _ctx.Participants.First().Name;
            InfoManager.Instance.OptionA = "N/A";
            InfoManager.Instance.OptionB = "Pass";
            InfoManager.Instance.OptionC = "SpecialAuction";
            InfoManager.Instance.OptionD = "N/A";
        }
    }
}