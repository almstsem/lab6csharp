using System;
using System.Collections.Generic;
using System.Xml.Serialization;

namespace Hospital.Business
{
    public enum Role { Doctor, Nurse }
    public enum PrescriptionKind { Medication, Diagnostic, Prophylactic }
    public enum MedForm { Injection, Tablet }

    public abstract class Entity
    {
        public Guid Id { get; set; } = Guid.NewGuid();
    }

    public class Patient : Entity
    {
        public string Name { get; set; } = "";
        public DateTime BirthDate { get; set; }
        public string Complaint { get; set; } = "";
    }

    public class Department : Entity
    {
        public string Name { get; set; } = "";
    }

    /// <summary>Персонал: врач или медсестра. DepartmentId == null — дежурный врач приёмного покоя.</summary>
    public class Staff : Entity
    {
        public string Name { get; set; } = "";
        public Role Role { get; set; }
        public Guid? DepartmentId { get; set; }
    }

    /// <summary>Назначение врача. Вид определяет, кто его выполняет и какие поля обязательны.</summary>
    public class Prescription : Entity
    {
        public PrescriptionKind Kind { get; set; }
        public string Description { get; set; } = "";   // препарат / процедура / исследование
        public MedForm? Form { get; set; }              // только для медикаментозных
        public int Quantity { get; set; }               // инъекции: количество
        public double DosageMg { get; set; }            // таблетки: дозировка
        public int Days { get; set; }                   // таблетки: дней приёма

        public bool Done { get; set; }
        public DateTime? DoneAt { get; set; }
        public string DoneBy { get; set; }

        /// <summary>Медикаментозные выполняет медсестра, остальные — врач.</summary>
        [XmlIgnore]
        public Role Performer
        {
            get { return Kind == PrescriptionKind.Medication ? Role.Nurse : Role.Doctor; }
        }

        public List<string> Validate()
        {
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(Description)) errors.Add("Не указано название назначения.");
            if (Kind == PrescriptionKind.Medication)
            {
                if (Form == null) errors.Add("Для лекарства укажите форму (инъекция/таблетки).");
                if (Form == MedForm.Injection && Quantity <= 0) errors.Add("Для инъекций укажите количество.");
                if (Form == MedForm.Tablet && (DosageMg <= 0 || Days <= 0))
                    errors.Add("Для таблеток укажите дозировку и количество дней.");
            }
            return errors;
        }

        public override string ToString()
        {
            if (Kind == PrescriptionKind.Medication)
                return Form == MedForm.Injection
                    ? Description + ": инъекции x" + Quantity
                    : Description + ": " + DosageMg + " мг, " + Days + " дн.";
            return (Kind == PrescriptionKind.Diagnostic ? "Диагностика: " : "Профилактика: ") + Description;
        }
    }

    /// <summary>ЭМК — электронная медицинская карта пациента (одна госпитализация).</summary>
    public class MedicalRecord : Entity
    {
        public Guid PatientId { get; set; }
        public Guid DepartmentId { get; set; }
        public Guid DoctorId { get; set; }          // лечащий врач
        public DateTime AdmittedAt { get; set; } = DateTime.Now;
        public DateTime? DischargedAt { get; set; }
        public string Diagnosis { get; set; } = ""; // диагноз при поступлении
        public List<Prescription> Prescriptions { get; set; } = new List<Prescription>();
    }
}
