using VoidlingGame;

namespace Voidling.Application.Ports;

public interface IGameStateRepository
{
    GameStateData? Load();
    void Save(GameStateData state);
}
