using System;

namespace collect_all.Services
{
    public static class DbConfig
    {
        // �Ш̧A�� MySQL �]�w�ק惡�r��
        public static string ConnectionString { get; set; } =
            "server=127.0.0.1;port=3306;database=systemmonitordb;user=users;password=users;";
    }
}