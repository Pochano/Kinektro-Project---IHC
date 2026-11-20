// Interfaz comun para cualquier control fisico de la consola DJ (boton, fader, knob)
// que pueda resaltarse visualmente cuando el pipeline le pide al jugador A que lo use.
// DJPipelineEvaluator busca este tipo en el objeto mapeado para cada DJControlTarget -
// no le importa si es un VR_Boton, un VR_Fader o un VR_Knob_Joystick, solo que sepa
// prenderse/apagarse.
public interface IResaltable
{
    void SetResaltado(bool activo);
    void SetProximidadColor(float cercania);
}
