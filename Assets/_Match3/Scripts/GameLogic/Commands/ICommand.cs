namespace Match3.GameLogic.Commands
{
    public interface ICommand
    {
        void Execute();
        void Undo();
    }
}
