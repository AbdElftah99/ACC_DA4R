namespace ACC_DA4R.Data
{
    public class Constants
    {
        public static string NickName
        {
            get
            {
                return Credentials.GetAppSetting("APS_CLIENT_ID");
            }
        }

        private static readonly char[] padding = { '=' };
    }
}
