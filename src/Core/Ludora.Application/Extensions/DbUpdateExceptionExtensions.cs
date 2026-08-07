using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Ludora.Application.Extensions
{
    public static class DbUpdateExceptionExtensions
    {
        public static bool IsUniqueConstraintViolation(this DbUpdateException exception)
        {
            return exception.InnerException is SqlException sqlException
                && sqlException.Number is 2601 or 2627;
        }
    }
}
