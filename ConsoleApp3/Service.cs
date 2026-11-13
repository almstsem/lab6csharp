using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Hospital.Business;
using Hospital.Data;

namespace Hospital.Services
{
    /// <summary>Фасад над бизнес-логикой и данными — единственная точка входа для GUI.</summary>
    public class HospitalService
    {
        private readonly IRepository<Patient> _patients;
        private readonly IRepository<Staff> _staff;
        private readonly IRepository<Department> _departments;
        private readonly IRepository<MedicalRecord> _records;

        public HospitalService(string dataDir)
        {
            _patients    = new XmlRepository<Patient>(dataDir);
            _staff       = new XmlRepository<Staff>(dataDir);
            _departments = new XmlRepository<Department>(dataDir);
            _records     = new XmlRepository<MedicalRecord>(dataDir);
            Seed();
        }

        // ---------- справочники ----------
        public IReadOnlyList<Department> Departments { get { return _departments.All(); } }
        public IReadOnlyList<Staff> AllStaff { get { return _staff.All(); } }

        public Staff DutyDoctor
        {
            get { return _staff.All().First(s => s.Role == Role.Doctor && s.DepartmentId == null); }
        }

        public List<Staff> DoctorsOf(Guid deptId)
        {
            return _staff.All().Where(s => s.Role == Role.Doctor && s.DepartmentId == deptId).ToList();
        }

        public List<MedicalRecord> ActiveRecords
        {
            get { return _records.All().Where(r => r.DischargedAt == null).ToList(); }
        }

        public string Title(MedicalRecord r)
        {
            var p = _patients.Get(r.PatientId);
            var d = _departments.Get(r.DepartmentId);
            var doc = _staff.Get(r.DoctorId);
            return (p == null ? "?" : p.Name) + " | " + (d == null ? "?" : d.Name) + " | врач " + (doc == null ? "?" : doc.Name);
        }

        // ---------- сценарии ----------
        /// <summary>Дежурный врач осматривает пациента, создаёт ЭМК и направляет в отделение.</summary>
        public MedicalRecord Admit(Patient p, string diagnosis, Guid departmentId, Guid attendingDoctorId)
        {
            if (string.IsNullOrWhiteSpace(p.Name)) throw new ArgumentException("Не указано ФИО пациента.");
            if (string.IsNullOrWhiteSpace(diagnosis)) throw new ArgumentException("Не указан диагноз при поступлении.");
            _patients.Add(p);
            var record = new MedicalRecord
            {
                PatientId = p.Id, DepartmentId = departmentId, DoctorId = attendingDoctorId, Diagnosis = diagnosis
            };
            _records.Add(record);
            return record;
        }

        public void Prescribe(MedicalRecord r, Prescription p)
        {
            var errors = p.Validate();
            if (errors.Count > 0) throw new ArgumentException(string.Join(" ", errors));
            r.Prescriptions.Add(p);
            _records.Save();
        }

        /// <summary>Выполнить назначение: лекарства — медсестра, остальное — врач. Запись попадает в ЭМК.</summary>
        public void Perform(MedicalRecord r, Prescription p, Staff who)
        {
            if (p.Done) throw new InvalidOperationException("Назначение уже выполнено.");
            if (who.Role != p.Performer)
                throw new InvalidOperationException("Это назначение выполняет: " + (p.Performer == Role.Nurse ? "медсестра" : "врач") + ".");
            p.Done = true; p.DoneAt = DateTime.Now; p.DoneBy = who.Name;
            _records.Save();
        }

        /// <summary>Выписка: возвращает выписной эпикриз.</summary>
        public string Discharge(MedicalRecord r)
        {
            r.DischargedAt = DateTime.Now;
            _records.Save();
            var p = _patients.Get(r.PatientId);
            int days = Math.Max(1, (r.DischargedAt.Value - r.AdmittedAt).Days);

            var sb = new StringBuilder();
            sb.AppendLine("=== ВЫПИСНОЙ ЭПИКРИЗ ===");
            sb.AppendLine("Пациент: " + p.Name + ", " + p.BirthDate.ToString("dd.MM.yyyy"));
            sb.AppendLine("Пребывание: " + r.AdmittedAt.ToString("dd.MM.yyyy") + " – " + r.DischargedAt.Value.ToString("dd.MM.yyyy") + " (" + days + " дн.)");
            sb.AppendLine("Диагноз при поступлении: " + r.Diagnosis);
            sb.AppendLine("Выполненные назначения:");
            foreach (var x in r.Prescriptions.Where(x => x.Done))
                sb.AppendLine("  - " + x + " (" + x.DoneAt.Value.ToString("dd.MM HH:mm") + ", " + x.DoneBy + ")");
            return sb.ToString();
        }

        private void Seed()
        {
            if (_departments.All().Count > 0) return;
            var cardio = new Department { Name = "Кардиологическое" };
            var surg   = new Department { Name = "Хирургическое" };
            var neuro  = new Department { Name = "Неврологическое" };
            _departments.Add(cardio); _departments.Add(surg); _departments.Add(neuro);

            _staff.Add(new Staff { Name = "Дежурный врач Орлов", Role = Role.Doctor });
            _staff.Add(new Staff { Name = "Иванов (кардиолог)", Role = Role.Doctor, DepartmentId = cardio.Id });
            _staff.Add(new Staff { Name = "Петрова (хирург)", Role = Role.Doctor, DepartmentId = surg.Id });
            _staff.Add(new Staff { Name = "Сидоров (невролог)", Role = Role.Doctor, DepartmentId = neuro.Id });
            _staff.Add(new Staff { Name = "Медсестра Кузнецова", Role = Role.Nurse });
            _staff.Add(new Staff { Name = "Медсестра Смирнова", Role = Role.Nurse });
        }
    }
}
