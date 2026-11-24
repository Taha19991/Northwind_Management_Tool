namespace Northwind.Core.Enums;

public static class EnumText
{
    // UserRole
    public static string ToDb(UserRole value) => value switch
    {
        UserRole.Manager => "Manager",
        UserRole.Sales => "Sales",
        UserRole.Logistics => "Logistics",
        UserRole.Support => "Support",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Invalid user role.")
    };

    public static UserRole ParseUserRole(string text) => text.Trim() switch
    {
        "Manager" => UserRole.Manager,
        "Sales" => UserRole.Sales,
        "Logistics" => UserRole.Logistics,
        "Support" => UserRole.Support,
        _ => throw new ArgumentException($"Unknown user role: '{text}'.", nameof(text))
    };

    public static string ToDisplay(UserRole value) => ToDb(value);

    // OrderStatus
    public static string ToDisplay(OrderStatus value) => value switch
    {
        OrderStatus.Pending => "Pending",
        OrderStatus.Shipped => "Shipped",
        OrderStatus.Late => "Late",
        _ => value.ToString()
    };

    // TicketStatus
    public static string ToDb(TicketStatus value) => value switch
    {
        TicketStatus.Open => "Open",
        TicketStatus.InProgress => "In progress",
        TicketStatus.Resolved => "Resolved",
        TicketStatus.Closed => "Closed",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Invalid ticket status.")
    };

    public static TicketStatus ParseTicketStatus(string text) => text.Trim() switch
    {
        "Open" => TicketStatus.Open,
        "In progress" => TicketStatus.InProgress,
        "Resolved" => TicketStatus.Resolved,
        "Closed" => TicketStatus.Closed,
        _ => throw new ArgumentException($"Unknown ticket status: '{text}'.", nameof(text))
    };

    public static string ToDisplay(TicketStatus value) => ToDb(value);

    // TicketCategory
    public static string ToDb(TicketCategory value) => value switch
    {
        TicketCategory.LateDelivery => "Late delivery",
        TicketCategory.DamagedProduct => "Damaged product",
        TicketCategory.WrongItem => "Wrong item",
        TicketCategory.Billing => "Billing",
        TicketCategory.Other => "Other",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Invalid ticket category.")
    };

    public static TicketCategory ParseTicketCategory(string text) => text.Trim() switch
    {
        "Late delivery" => TicketCategory.LateDelivery,
        "Damaged product" => TicketCategory.DamagedProduct,
        "Wrong item" => TicketCategory.WrongItem,
        "Billing" => TicketCategory.Billing,
        "Other" => TicketCategory.Other,
        _ => throw new ArgumentException($"Unknown ticket category: '{text}'.", nameof(text))
    };

    public static string ToDisplay(TicketCategory value) => ToDb(value);

    // TicketPriority
    public static string ToDb(TicketPriority value) => value switch
    {
        TicketPriority.Low => "Low",
        TicketPriority.Medium => "Medium",
        TicketPriority.High => "High",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Invalid ticket priority.")
    };

    public static TicketPriority ParseTicketPriority(string text) => text.Trim() switch
    {
        "Low" => TicketPriority.Low,
        "Medium" => TicketPriority.Medium,
        "High" => TicketPriority.High,
        _ => throw new ArgumentException($"Unknown ticket priority: '{text}'.", nameof(text))
    };

    public static string ToDisplay(TicketPriority value) => ToDb(value);

    // ReturnStatus
    public static string ToDb(ReturnStatus value) => value switch
    {
        ReturnStatus.Requested => "Requested",
        ReturnStatus.Approved => "Approved",
        ReturnStatus.Rejected => "Rejected",
        ReturnStatus.Refunded => "Refunded",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Invalid return status.")
    };

    public static ReturnStatus ParseReturnStatus(string text) => text.Trim() switch
    {
        "Requested" => ReturnStatus.Requested,
        "Approved" => ReturnStatus.Approved,
        "Rejected" => ReturnStatus.Rejected,
        "Refunded" => ReturnStatus.Refunded,
        _ => throw new ArgumentException($"Unknown return status: '{text}'.", nameof(text))
    };

    public static string ToDisplay(ReturnStatus value) => ToDb(value);

    // ReturnReason
    public static string ToDb(ReturnReason value) => value switch
    {
        ReturnReason.Damaged => "Damaged",
        ReturnReason.WrongItem => "Wrong item",
        ReturnReason.Expired => "Expired",
        ReturnReason.NotNeeded => "Not needed",
        ReturnReason.Other => "Other",
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Invalid return reason.")
    };

    public static ReturnReason ParseReturnReason(string text) => text.Trim() switch
    {
        "Damaged" => ReturnReason.Damaged,
        "Wrong item" => ReturnReason.WrongItem,
        "Expired" => ReturnReason.Expired,
        "Not needed" => ReturnReason.NotNeeded,
        "Other" => ReturnReason.Other,
        _ => throw new ArgumentException($"Unknown return reason: '{text}'.", nameof(text))
    };

    public static string ToDisplay(ReturnReason value) => ToDb(value);

    // Generic helpers for Dapper TypeHandler
    public static string ToDb<TEnum>(TEnum value) where TEnum : struct, Enum => value switch
    {
        UserRole v => ToDb(v),
        TicketStatus v => ToDb(v),
        TicketCategory v => ToDb(v),
        TicketPriority v => ToDb(v),
        ReturnStatus v => ToDb(v),
        ReturnReason v => ToDb(v),
        _ => value.ToString()
    };

    public static TEnum FromDb<TEnum>(string text) where TEnum : struct, Enum
    {
        object result = typeof(TEnum) switch
        {
            var t when t == typeof(UserRole) => ParseUserRole(text),
            var t when t == typeof(TicketStatus) => ParseTicketStatus(text),
            var t when t == typeof(TicketCategory) => ParseTicketCategory(text),
            var t when t == typeof(TicketPriority) => ParseTicketPriority(text),
            var t when t == typeof(ReturnStatus) => ParseReturnStatus(text),
            var t when t == typeof(ReturnReason) => ParseReturnReason(text),
            _ => Enum.Parse<TEnum>(text, ignoreCase: true)
        };
        return (TEnum)result;
    }
}
