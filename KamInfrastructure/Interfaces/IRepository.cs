using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KamInfrastructure.Interfaces
{
    public interface IRepository<T>
    {
        Task<T?> GetById(int id);
        Task<List<T>> GetAll();
        Task Add(T entity);
        void Update(T entity);
        void Delete(T entity);
        Task<T?> GetByCredentials(string username, string password);
    }
}
