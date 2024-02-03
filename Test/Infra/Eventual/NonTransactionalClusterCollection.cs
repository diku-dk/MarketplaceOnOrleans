namespace Test.Infra.Eventual;

[CollectionDefinition(Name)]
public class NonTransactionalClusterCollection : ICollectionFixture<NonTransactionalClusterFixture>
{
    public const string Name = "NonTransactionalClusterCollection";
}

