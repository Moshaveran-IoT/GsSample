using Domain.Features;
using Domain.Models;
using Infrastructure;
using Infrastructure.Database;
using Infrastructure.Extensions;
using Infrastructure.Interfaces;
using Infrastructure.Repositories;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace API;

/// <summary>
/// کلاس برای تست Person Feature
/// </summary>
public static class TestPersonFeature
{
    /// <summary>
    /// اجرای تست کامل Person Feature
    /// </summary>
    public static async Task<bool> RunTestAsync(
        IConfiguration configuration,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            logger?.LogInformation("🚀 Starting Person Feature Test...");

            // 1. Setup Database
            logger?.LogInformation("\n📋 Step 1: Testing Database Connection...");
            var dbSetupSuccess = await TestDatabaseConnection.TestAndSetupDatabaseAsync(
                configuration, logger, cancellationToken);
            
            if (!dbSetupSuccess)
            {
                logger?.LogError("❌ Database setup failed!");
                return false;
            }

            // 2. Setup DI Container
            logger?.LogInformation("\n📋 Step 2: Setting up DI Container...");
            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(configuration);
            services.AddLogging(builder => builder.AddConsole());
            services.AddConnectionFactory(configuration);
            services.AddRepositories();
            services.AddMediatR(typeof(Application.GetAllPersonQueryHandler).Assembly);

            var serviceProvider = services.BuildServiceProvider();
            logger?.LogInformation("✅ DI Container setup complete!");

            // 3. Test Repository
            logger?.LogInformation("\n📋 Step 3: Testing PersonRepository...");
            var personRepository = serviceProvider.GetRequiredService<IPersonRepository>();
            
            // 3.1. Create Person
            logger?.LogInformation("  → Creating a new Person...");
            var newPerson = new Person
            {
                FirstName = "Test",
                LastName = "User",
                DateOfBirth = new DateTime(1990, 1, 1)
            };
            
            await personRepository.CreatePerson(newPerson, cancellationToken);
            logger?.LogInformation("  ✅ Person created with ID: {Id}", newPerson.Id);

            // 3.2. Get Person by ID
            logger?.LogInformation("  → Getting Person by ID: {Id}...", newPerson.Id);
            var retrievedPerson = await personRepository.GetById(newPerson.Id, cancellationToken);
            logger?.LogInformation("  ✅ Person retrieved: {FirstName} {LastName}", 
                retrievedPerson.FirstName, retrievedPerson.LastName);

            // 3.3. Update Person
            logger?.LogInformation("  → Updating Person...");
            retrievedPerson.FirstName = "Updated";
            retrievedPerson.LastName = "Test User";
            await personRepository.UpdatePerson(retrievedPerson.Id, retrievedPerson, cancellationToken);
            logger?.LogInformation("  ✅ Person updated successfully!");

            // 3.4. Get All Persons
            logger?.LogInformation("  → Getting all Persons...");
            var allPersons = await personRepository.GetAll(cancellationToken);
            logger?.LogInformation("  ✅ Retrieved {Count} person(s)", allPersons.Count());

            // 4. Test MediatR Handlers
            logger?.LogInformation("\n📋 Step 4: Testing MediatR Handlers...");
            var mediator = serviceProvider.GetRequiredService<IMediator>();

            // 4.1. Test GetAllPersonQuery
            logger?.LogInformation("  → Testing GetAllPersonQuery...");
            var getAllQuery = new GetAllPersonQuery();
            var getAllResponse = await mediator.Send(getAllQuery, cancellationToken);
            logger?.LogInformation("  ✅ GetAllPersonQuery returned {Count} person(s)", 
                getAllResponse.Persons.Count());

            // 4.2. Test GetPersonByIdQuery
            logger?.LogInformation("  → Testing GetPersonByIdQuery...");
            var getByIdQuery = new GetPersonByIdQuery(newPerson.Id);
            var getByIdResponse = await mediator.Send(getByIdQuery, cancellationToken);
            if (getByIdResponse.Person != null)
            {
                logger?.LogInformation("  ✅ GetPersonByIdQuery returned: {FirstName} {LastName}", 
                    getByIdResponse.Person.FirstName, getByIdResponse.Person.LastName);
            }

            // 4.3. Test CreatePersonCommand
            logger?.LogInformation("  → Testing CreatePersonCommand...");
            var createCommand = new CreatePersonCommand(new Person
            {
                FirstName = "MediatR",
                LastName = "Test",
                DateOfBirth = new DateTime(1995, 5, 15)
            });
            var createResponse = await mediator.Send(createCommand, cancellationToken);
            logger?.LogInformation("  ✅ CreatePersonCommand created Person with ID: {Id}", createResponse.Id);

            // 4.4. Test UpdatePersonCommand
            logger?.LogInformation("  → Testing UpdatePersonCommand...");
            var updateCommand = new UpdatePersonCommand(createResponse.Id, new Person
            {
                FirstName = "Updated",
                LastName = "MediatR Test",
                DateOfBirth = new DateTime(1995, 5, 15)
            });
            await mediator.Send(updateCommand, cancellationToken);
            logger?.LogInformation("  ✅ UpdatePersonCommand updated Person successfully!");

            // 4.5. Test DeletePersonCommand
            logger?.LogInformation("  → Testing DeletePersonCommand...");
            var deleteCommand = new DeletePersonCommand(createResponse.Id);
            var deleteResponse = await mediator.Send(deleteCommand, cancellationToken);
            logger?.LogInformation("  ✅ DeletePersonCommand deleted Person: {Success}", deleteResponse.Success);

            // Cleanup - Delete test person (only if it was created by test)
            logger?.LogInformation("\n📋 Step 5: Cleaning up test data...");
            try
            {
                // فقط person ایجاد شده در تست را حذف می‌کنیم
                // داده‌های نمونه باقی می‌مانند
                if (newPerson.Id > 0)
                {
                    await personRepository.DeletePerson(newPerson.Id, cancellationToken);
                    logger?.LogInformation("  ✅ Test person cleaned up! (Sample data preserved)");
                }
            }
            catch (Exception ex)
            {
                logger?.LogWarning("  ⚠️ Could not clean up test data: {Message}", ex.Message);
            }

            // نمایش تعداد کل records
            var finalCount = await personRepository.GetAll(cancellationToken);
            logger?.LogInformation("\n📊 Final record count in database: {Count}", finalCount.Count());

            logger?.LogInformation("\n🎉 All tests passed successfully!");
            return true;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, "❌ Test failed: {Message}", ex.Message);
            return false;
        }
    }
}

