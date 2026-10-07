using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace VrGame.Domain.Economy
{
    public sealed class EconomySnapshot
    {
        internal EconomySnapshot(int balance, long version, IEnumerable<BalanceChanged> ledger)
        {
            Balance = balance;
            Version = version;
            Ledger = new ReadOnlyCollection<BalanceChanged>(ledger.ToList());
        }

        public int Balance { get; }

        public long Version { get; }

        public IReadOnlyList<BalanceChanged> Ledger { get; }
    }
}
