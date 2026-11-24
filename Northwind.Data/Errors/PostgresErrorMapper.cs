using System.Net.Sockets;
using Northwind.Core.Exceptions;
using Npgsql;

namespace Northwind.Data.Errors;

public static class PostgresErrorMapper
{
    private const string ConnectionErrorMessage =
        "Cannot connect to the database. Check that PostgreSQL is running and the connection settings are correct.";

    public static UserFriendlyException ToUserFriendlyException(PostgresException ex) =>
        new(ToUserMessage(ex), ex);

    public static string ToUserMessage(PostgresException ex)
    {
        var constraintMessage = ex.ConstraintName?.ToLowerInvariant() switch
        {
            "pk_customers" => "This customer ID is already in use.",
            "uq_products_name" => "This product name is already in use.",
            "ck_products_price" => "Unit price cannot be negative.",
            "ck_products_stock" => "Insufficient stock.",
            "ck_orders_shipped" => "Shipped date cannot be earlier than the order date.",
            "ck_orders_freight" => "Freight cannot be negative.",
            "ck_od_qty" => "Quantity must be greater than zero.",
            "ck_od_discount" => "Discount must be between 0% and 100%.",
            "pk_order_details" => "This product is already on the order.",
            "ck_ticket_order_xor_customer" => "A ticket must be linked to either an order or a customer, not both.",
            "ck_ticket_resolved" => "The resolved time cannot be earlier than the creation time.",
            _ => null
        };

        if (constraintMessage is not null)
        {
            return constraintMessage;
        }

        return ex.SqlState switch
        {
            PostgresErrorCodes.ForeignKeyViolation
                when string.Equals(ex.TableName, "product_returns", StringComparison.OrdinalIgnoreCase) =>
                "The selected order line does not exist.",

            PostgresErrorCodes.ForeignKeyViolation =>
                "This record is used by other records and cannot be changed or deleted.",

            PostgresErrorCodes.UniqueViolation =>
                "This record already exists.",

            PostgresErrorCodes.CheckViolation
                when ex.MessageText?.StartsWith("Return quantity", StringComparison.Ordinal) == true =>
                ex.MessageText,

            PostgresErrorCodes.CheckViolation =>
                string.IsNullOrWhiteSpace(ex.ConstraintName) && !string.IsNullOrWhiteSpace(ex.MessageText)
                    ? $"The value does not satisfy a business rule: {ex.MessageText}"
                    : "The value does not satisfy a business rule.",

            PostgresErrorCodes.InvalidPassword
                or PostgresErrorCodes.InvalidAuthorizationSpecification
                or PostgresErrorCodes.InvalidCatalogName => ConnectionErrorMessage,

            PostgresErrorCodes.InsufficientPrivilege =>
                "You do not have database permission for this operation.",

            _ => "An unexpected database error occurred."
        };
    }

    public static string ToUserMessage(Exception ex) => ex switch
    {
        PostgresException pgEx => ToUserMessage(pgEx),
        NpgsqlException or SocketException or TimeoutException => ConnectionErrorMessage,
        _ => "An unexpected error occurred."
    };
}
