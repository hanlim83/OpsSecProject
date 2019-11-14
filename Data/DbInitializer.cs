namespace OpsSecProject.Data
{
    public class DbInitializer
    {
        public static void InitializeLogDataContext(LogDataContext context)
        {
            context.Database.EnsureCreated();
        }
    }
}
