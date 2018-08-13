namespace Unosquare.FFME.Primitives
{
    using System;

    /// <summary>
    /// Represents a method that can be awaited via its Awaiter object.
    /// </summary>
    /// <seealso cref="PromiseBase" />
    public class Promise : PromiseBase
    {
        private readonly Action DeferredAction;

        /// <summary>
        /// Initializes a new instance of the <see cref="Promise"/> class.
        /// </summary>
        /// <param name="deferredAction">The deferred action.</param>
        /// <param name="continueOnCapturedContext">
        /// if set to <c>true</c> configures the awaiter to continue on the captured context.
        /// </param>
        public Promise(Action deferredAction, bool continueOnCapturedContext)
            : base(continueOnCapturedContext) => DeferredAction = deferredAction;

        /// <summary>
        /// Initializes a new instance of the <see cref="Promise"/> class.
        /// </summary>
        /// <param name="deferredAction">The deferred action.</param>
        public Promise(Action deferredAction)
            : this(deferredAction, true) { }

        /// <summary>
        /// Initializes a new instance of the <see cref="Promise"/> class.
        /// </summary>
        public Promise()
            : this(() => { }, true) { }

        /// <summary>
        /// Performs the actions represented by this deferred task.
        /// </summary>
        protected override void PerformActions() => DeferredAction();
    }
}
