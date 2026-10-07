namespace VrGame.Domain.Economy
{
    public interface IEconomyEventSink
    {
        // Координатор транзакции может буферизовать событие до общего commit.
        // Реализация не должна синхронно запускать новую mutation этого сервиса.
        bool TryPublish(BalanceChanged balanceChanged);
    }
}
