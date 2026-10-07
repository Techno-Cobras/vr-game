namespace VrGame.Domain.Economy
{
    public sealed class EconomySnapshot
    {
        internal EconomySnapshot(int balance, long version)
        {
            Balance = balance;
            Version = version;
        }

        public int Balance { get; }

        public long Version { get; }
    }
}
