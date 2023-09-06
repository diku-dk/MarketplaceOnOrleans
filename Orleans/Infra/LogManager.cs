namespace Orleans.Infra;

public interface ILogManager
{
    Task WriteLog(byte[] bytes);
}

public class LogManager : ILogManager
{


    public LogManager() { }

    public Task WriteLog(byte[] bytes)
    {
        throw new NotImplementedException();
    }
}