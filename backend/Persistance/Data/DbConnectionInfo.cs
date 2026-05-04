namespace Persistance.Data
{
    public class DbConnectionInfo
    {
        public string Host { get; set; }
        public string Database { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public override string ToString()
        {
            string output = "Host=" + Host
                + ";Database=" + Database
                + ";Username=" + Username
                + ";Password=" + Password;

            return output;
        }
    }
}
