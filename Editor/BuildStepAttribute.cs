using System;

namespace noio.MultiBuild
{
    public class BuildStepAttribute : Attribute
    {
        public BuildStepAttribute(BuildStepOrder order, bool allowMultiple)
        {
            Order = order;
            AllowMultiple = allowMultiple;
        }

        #region PROPERTIES

        public BuildStepOrder Order { get; }
        public bool AllowMultiple { get; }

        #endregion
    }
}