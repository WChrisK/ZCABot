namespace ZCABot
{
    public class Start
    {
        public static void Main()
        {
            new Bot().RunAsync().GetAwaiter().GetResult();
        }
    }
}
