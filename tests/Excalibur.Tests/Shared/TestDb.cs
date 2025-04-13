using System.Data;

using Excalibur.DataAccess;
using Excalibur.Domain;

namespace Excalibur.Tests.Shared;

public sealed class TestDb(IDbConnection connection) : Db(connection), IDomainDb;
