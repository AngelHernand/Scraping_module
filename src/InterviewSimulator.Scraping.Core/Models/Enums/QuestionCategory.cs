namespace InterviewSimulator.Scraping.Core.Models.Enums;

// Categoría de la pregunta de entrevista laboral.
public enum QuestionCategory
{
    //Preguntas sobre tecnologías, código, arquitectura, algoritmos
    Technical = 0,

    //Preguntas sobre comportamiento, trabajo en equipo, conflictos
    Behavioral = 1,

    //Preguntas hipotéticas: "¿Qué harías si...?"
    Situational = 2,

    //Preguntas generales: "Háblame de ti", "¿Por qué esta empresa?"
    General = 3,

    //No se pudo clasificar automáticamente
    Unknown = 99
}
