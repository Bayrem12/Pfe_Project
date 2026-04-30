using Application.Interfaces;
using Domain.Interfaces;

namespace Persistance.Data
{
    public class TestAutoumatisationUnitOfWork : UnitOfWork<TestAutoumatisationContext>, ITestAutoumatisationUnitOfWork
    {
        public TestAutoumatisationUnitOfWork(IContext context) : base(context)
        {
        }
    }
}
