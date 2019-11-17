namespace OpsSecProject.Data
{
    public class DbInitializer
    {
        public static void InitializeLogDataContext(AuthenticationContext context)
        {
            context.Database.EnsureCreated();
        }
    }
}
