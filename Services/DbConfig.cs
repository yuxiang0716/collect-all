using System;

namespace collect_all.Services
{
    public static class DbConfig
    {
        // 請依你的 MySQL 設定修改此字串
        public static string ConnectionString { get; set; } =
            "server=127.0.0.1;port=3306;database=systemmonitordb;user=users;password=users;";
    }
}