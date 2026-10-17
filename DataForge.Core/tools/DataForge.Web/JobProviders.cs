using DataForge.Jobs.DuckDB;
using DataForge.Jobs.Excel;
using DataForge.Jobs.Http;
using DataForge.Jobs.MySql;
using DataForge.Jobs.Parquet;
using DataForge.Jobs.PostgreSql;
using DataForge.Jobs.Sqlite;
using DataForge.Jobs.SqlServer;

namespace DataForge.Web;

public static class JobProviders
{
    public static void RegisterAll()
    {
        JobSqliteRegistration.AddDataForgeJobsSqlite();
        JobPostgreSqlRegistration.AddDataForgeJobsPostgreSql();
        JobSqlServerRegistration.AddDataForgeJobsSqlServer();
        JobParquetRegistration.AddDataForgeJobsParquet();
        JobDuckDBRegistration.AddDataForgeJobsDuckDB();
        JobMySqlRegistration.AddDataForgeJobsMySql();
        JobExcelRegistration.AddDataForgeJobsExcel();
        JobHttpRegistration.AddDataForgeJobsHttp();
    }
}
