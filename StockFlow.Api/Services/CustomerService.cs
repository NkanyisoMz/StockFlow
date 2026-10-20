using Microsoft.EntityFrameworkCore;
using Npgsql;
using StockFlow.Api.DTOs;

public class CustomerService
{
    private readonly StockFlowDbContext _db;

    public CustomerService(StockFlowDbContext db)
    {
        _db = db;
    }

    public async Task<Customer?> GetByIdAsync(int id)
    {
        var customer = await _db.Customers.FindAsync(id);

        return customer;
    }

    public async Task<Customer> CreateAsync(CreateCustomerDto dto)
    {
        string normalizedEmail = dto.Email.Trim().ToLowerInvariant();

        var emailExists = await _db.Customers.AnyAsync(c => c.Email == normalizedEmail);

        if (emailExists)
        {
            throw new DuplicateEmailException($"A customer with the email '{normalizedEmail}' already exists.");
        }

        var customer = new Customer
        {
            Name = dto.Name.Trim(),
            Email = normalizedEmail
        };

        _db.Customers.Add(customer);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is PostgresException pgEx &&
            pgEx.SqlState == PostgresErrorCodes.UniqueViolation &&
            pgEx.ConstraintName == "IX_Customers_Email")
        {
            throw new DuplicateEmailException(
                $"A customer with the email '{normalizedEmail}' already exists.");
        }

        return customer;
    }

    public async Task<List<Customer>> GetAllAsync()
    {
    return await _db.Customers
        .AsNoTracking()
        .ToListAsync();
    }

    public async Task<bool> UpdateAsync(int id, UpdateCustomerDto dto)
    {
        var customer = await _db.Customers.FindAsync(id);

        if (customer is null)
        {
            return false;
        }

        string normalizedEmail = dto.Email.Trim().ToLowerInvariant();

        var emailExists = await _db.Customers.AnyAsync(
        c => c.Email == normalizedEmail && c.Id != id);

        if (emailExists)
        {
            throw new DuplicateEmailException($"The email '{normalizedEmail}' is already taken by another customer.");
        }

        customer.Name = dto.Name.Trim();
        customer.Email = normalizedEmail;

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
            when (ex.InnerException is PostgresException pgEx &&
            pgEx.SqlState == PostgresErrorCodes.UniqueViolation &&
            pgEx.ConstraintName == "IX_Customers_Email")
        {
            throw new DuplicateEmailException(
                $"The email '{normalizedEmail}' is already taken by another customer.");
        }

        return true;
    }

}
