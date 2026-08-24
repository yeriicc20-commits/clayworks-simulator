Backup tomado el 2026-08-22 antes de reescribir las fisicas de agarre de la garra (ClawController.cs) hacia un sistema de joints por dedo (ConfigurableJoint + fuerza realista).

Contenido:
- ClawController.cs.bak -> version anterior completa (sistema de un unico FixedJoint entre clawHead y el peluche).
- PlushItem.cs.bak -> version anterior (sin cambios reales, guardada por precaucion).

Como restaurar si algo falla:
1. Copia el contenido de ClawController.cs.bak sobre Assets/_Project/Scripts/Machines/ClawController.cs (quitando el .bak).
2. Copia el contenido de PlushItem.cs.bak sobre Assets/_Project/Scripts/Machines/PlushItem.cs si hiciera falta.
3. Guarda y deja que Unity recompile.

Alternativa sin restaurar archivos:
El nuevo ClawController.cs incluye el campo publico "useRealisticGripPhysics" (Inspector, componente ClawController).
Desmarcalo para volver al comportamiento antiguo (FixedJoint unico, currentGripStrength) sin tocar ningun archivo,
ya que el codigo antiguo se mantuvo intacto dentro del mismo script (TryGrabPlush, ReleasePlush, MonitorGripLoss).
