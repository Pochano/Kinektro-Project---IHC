# Guía de Git para el Proyecto Kinektro

Instrucciones rápidas para mantener sincronizado el proyecto de Unity entre el equipo.

---


## 1. Bajar nuevos avances

Ejecuta este comando antes de empezar a trabajar para traer los cambios más recientes:

```bash
git pull origin main
```

Verificación: Abre Unity o ejecuta git status para confirmar que tu versión local está actualizada sin conflictos.


2. Subir avances a GitHub

Al terminar una sesión de trabajo o añadir un nuevo avance, sigue estos 3 pasos en Git Bash:

Paso A: Preparar los cambios

```Bash
git add .
```

Verificación: Escribe git status y confirma que las carpetas modificadas (como Assets/) aparezcan destacadas en verde.

Paso B: Crear la nota de guardado

```Bash
git commit -m "Descripción corta de lo que avanzaste" 
```

Verificación: Revisa que en la terminal aparezca el resumen de archivos confirmados para el commit.

Paso C: Enviar a GitHub

```Bash
git push origin main
```
Verificación: Entra a la página de tu repositorio en GitHub y refresca para confirmar que tu mensaje de commit ya está publicado.

