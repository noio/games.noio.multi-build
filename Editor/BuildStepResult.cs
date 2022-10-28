namespace noio.MultiBuild
{
    public readonly struct BuildStepResult
    {
        public enum ResultType
        {
            Success = 0,
            Warning = 1,
            Error = 2
        }

        public BuildStepResult(ResultType type = ResultType.Success, string message = null)
        {
            Type = type;
            Message = message;
        }

        public ResultType Type { get; }
        public string Message { get; }
    }
}