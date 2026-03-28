namespace VendaZap.Domain.ValueObjects;

public record Money(decimal Amount, string Currency)
{
    public string FormatBRL() => Amount.ToString("C", new System.Globalization.CultureInfo("pt-BR"));

    public static Money Zero(string currency = "BRL") => new(0, currency);

    public static Money operator +(Money a, Money b)
    {
        if (a.Currency != b.Currency) throw new InvalidOperationException("Cannot add different currencies.");
        return new Money(a.Amount + b.Amount, a.Currency);
    }

    public static Money operator *(Money m, int quantity) => new(m.Amount * quantity, m.Currency);
}

public record PhoneNumber(string Value)
{
    public static PhoneNumber Parse(string raw)
    {
        var cleaned = new string(raw.Where(char.IsDigit).ToArray());
        if (cleaned.Length < 10 || cleaned.Length > 13)
            throw new ArgumentException("Invalid phone number format.");
        // Ensure Brazil country code
        if (!cleaned.StartsWith("55")) cleaned = "55" + cleaned;
        return new PhoneNumber(cleaned);
    }

    public string ToWhatsAppFormat() => Value;
    public override string ToString() => Value;
}

public record Address(
    string Street,
    string Number,
    string? Complement,
    string City,
    string State,
    string ZipCode)
{
    public string Format() => $"{Street}, {Number}{(Complement != null ? $", {Complement}" : "")} - {City}/{State} - CEP: {ZipCode}";
}
