using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using Hospital.Business;

namespace Hospital.Data
{
    public interface IRepository<T> where T : Entity
    {
        IReadOnlyList<T> All();
        T Get(Guid id);
        void Add(T item);
        void Save();            // сохранить изменения (после правки объекта)
    }

    /// <summary>Один универсальный репозиторий на все сущности: хранит список в XML-файле.</summary>
    public class XmlRepository<T> : IRepository<T> where T : Entity
    {
        private static readonly XmlSerializer Serializer = new XmlSerializer(typeof(List<T>));
        private readonly string _path;
        private readonly List<T> _items = new List<T>();

        public XmlRepository(string dir)
        {
            Directory.CreateDirectory(dir);
            _path = Path.Combine(dir, typeof(T).Name + ".xml");
            if (File.Exists(_path))
                using (var s = File.OpenRead(_path))
                    _items = (List<T>)Serializer.Deserialize(s);
        }

        public IReadOnlyList<T> All() { return _items; }
        public T Get(Guid id) { return _items.FirstOrDefault(x => x.Id == id); }
        public void Add(T item) { _items.Add(item); Save(); }

        public void Save()
        {
            using (var s = File.Create(_path))
                Serializer.Serialize(s, _items);
        }
    }
}
