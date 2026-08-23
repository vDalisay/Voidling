using System.Linq;

namespace VoidlingGame;

public partial class GameSession
{
    public void BuyStoreEgg(string eggId)
    {
        var egg = State.StoreEggs.FirstOrDefault(e => e.Id == eggId);
        if (egg == null)
            return;

        if (State.Coins < GameRules.StoreEggPrice)
        {
            ToastRequested?.Invoke("Not enough sprouts.");
            return;
        }

        State.Coins -= GameRules.StoreEggPrice;
        State.StoreEggs.Remove(egg);
        egg.Source = EggSource.Store;
        egg.IncubationSeconds = 0.0f;

        var nestPosition = NextNestPosition();
        egg.WorldX = nestPosition.X;
        egg.WorldY = nestPosition.Y;
        State.OwnedEggs.Add(egg);
        State.StoreEggs.Add(CreateStoreEgg());
        SaveAndNotify("Bought a mystery egg.");
    }
}
