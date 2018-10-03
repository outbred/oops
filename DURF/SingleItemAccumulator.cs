using System;
using System.Diagnostics;

namespace DURF 
{
    [DebuggerDisplay("{Name} with {Records.Count} records")]
    public class SingleItemAccumulator : Accumulator
    {

        public static void Track(string name, Action undo)
        {
            var acc = new SingleItemAccumulator(name, undo);
            acc.ClosedForBusiness = true;
            AccumulatorManager.Instance.Add(acc, ScopeState.Do);
        }

        private bool ClosedForBusiness { get; set; }

        private SingleItemAccumulator(string name, Action undo) : base(name)
        {
            this.AddUndo(undo);
            ClosedForBusiness = true;
            AccumulatorManager.Instance.Add(this, ScopeState.Do);
        }

        #region Overrides of Accumulator

        /// <inheritdoc />
        public override void AddUndo(Action onUndo, string propertyName = null, object instance = null)
        {
            if(ClosedForBusiness)
                throw new InvalidOperationException("This accumulator has already recorded a single undo. Make a new one!");

            base.AddUndo(onUndo, propertyName, instance);
        }

        #endregion
    }
}