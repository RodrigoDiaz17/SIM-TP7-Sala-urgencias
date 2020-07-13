﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using GeneradorDeNumerosAleatorios;
using RandomVarGenerator;
using SimulacionMontecarlo;
using Sistemas_dinamicos;

namespace SimulacionMontecarlo
{
    class Simulator
    {
        bool DEBUG = true;
        public UniformGenerator uniformGeneratorAterrizaje { get; set; }
        public UniformGenerator uniformGeneratorDespegue { get; set; }
        public ConvolutionGenerator convolutionGenerator { get; set; }
        public ExponentialGenerator exponentialGenerator { get; set; }
        public BoxMullerGenerator boxMullerGenerator { get; set; }
        public Generator generator { get; set; }

        public List<Avion> clientes { get; set; }



        public Simulator()
        {
            uniformGeneratorAterrizaje = new UniformGenerator();
            uniformGeneratorAterrizaje.a = 3;
            uniformGeneratorAterrizaje.b = 5;
            uniformGeneratorDespegue = new UniformGenerator();
            uniformGeneratorDespegue.a = 2;
            uniformGeneratorDespegue.b = 4;
            convolutionGenerator = new ConvolutionGenerator();
            exponentialGenerator = new ExponentialGenerator();
            exponentialGenerator.lambda = (double)0.1;
            convolutionGenerator.mean = 80;
            convolutionGenerator.stDeviation = 30;
            generator = new Generator();
        }

        public StateRow NextStateRow(StateRow anterior, int i)
        {
            // Creo vector de estado para fila actual
            StateRow actual = new StateRow();

            // Diccionario de eventos (nombre de evento y su tiempo) para determinar el siguiente evento
            Dictionary<string, double> tiempos = new Dictionary<string, double>();
            var siguienteEvento = this.DeterminarSiguienteEvento(tiempos, anterior);

            int avion = 0;
            switch (siguienteEvento.Key)
            {
                case "tiempoProximaLlegada":
                    actual = CrearStateRowLlegadaAvion(anterior, siguienteEvento.Value, i);
                    break;
                case var val when new Regex(@"tiempoFinAterrizaje_*").IsMatch(val):
                    avion = Convert.ToInt32(siguienteEvento.Key.Split('_')[1]);
                    actual = CrearStateRowFinAterrizaje(anterior, siguienteEvento.Value, avion);
                    break;
                case var val when new Regex(@"tiempoFinDeDespegue_*").IsMatch(val):
                    avion = Convert.ToInt32(siguienteEvento.Key.Split('_')[1]);
                    actual = CrearStateRowFinDeDespegue(anterior, siguienteEvento.Value, avion);
                    break;
                case var val when new Regex(@"tiempoPermanencia_*").IsMatch(val):
                    avion = Convert.ToInt32(siguienteEvento.Key.Split('_')[1]);
                    actual = CrearStateRowFinDePermanencia(anterior, siguienteEvento.Value, avion);
                    break;
                case "tiempoInestabilidad":
                    actual = CrearStateRowInicioInestabilidad(anterior, siguienteEvento.Value);
                    break;
                case "tiempoFinPurga":
                    actual = CrearStateRowFinPurga(anterior, siguienteEvento.Value);
                    break;
            }

            actual.iterationNum = i + 1;

            return actual;
        }

        public KeyValuePair<string, double> DeterminarSiguienteEvento(Dictionary<string, double> tiempos, StateRow anterior)
        {
            if (anterior.tiempoProximaLlegada != 0)
                tiempos.Add("tiempoProximaLlegada", anterior.tiempoProximaLlegada);

            if (anterior.tiempoInestabilidad != 0)
                tiempos.Add("tiempoInestabilidad", anterior.tiempoInestabilidad);

            if (anterior.tiempoFinPurga != 0)
                tiempos.Add("tiempoFinPurga", anterior.tiempoFinPurga);

            // Checkear optimizacion (desde - hasta)
            for (int j = 0; j < this.clientes.Count; j++)
            {
                if (this.clientes[j].tiempoFinAterrizaje != 0)
                    tiempos.Add("tiempoFinAterrizaje_" + (j + 1).ToString(), this.clientes[j].tiempoFinAterrizaje);
                if (this.clientes[j].tiempoFinDeDespegue != 0)
                    tiempos.Add("tiempoFinDeDespegue_" + (j + 1).ToString(), this.clientes[j].tiempoFinDeDespegue);
                if (this.clientes[j].tiempoPermanencia != 0)
                    tiempos.Add("tiempoPermanencia_" + (j + 1).ToString(), this.clientes[j].tiempoPermanencia);
            }

            var tiemposOrdenados = tiempos.OrderBy(obj => obj.Value).ToDictionary(obj => obj.Key, obj => obj.Value);
            KeyValuePair<string, double> menorTiempo = tiemposOrdenados.First();

            return menorTiempo;
        }

        private int GetCantidadAvionesEnPermanencia()
        {
            int contador = 0;
            for (int i = 0; i < this.clientes.Count; i++)
            {
                if (this.clientes[i].estado == "EP")
                    contador += 1;
            }
            return contador;
        }

        private StateRow CrearStateRowLlegadaAvion(StateRow anterior, double tiempoProximoEvento, int i)
        {
            StateRow nuevo = new StateRow();

            // Controlamos que los aviones en tierra sean menores a 30, si lo son, pasamos al siguiente menor tiempo, es decir, el siguiente evento
            int cantAvionesEnPermanencia = GetCantidadAvionesEnPermanencia();
            if (anterior.pista.colaEET.Count + anterior.pista.colaEEV.Count + cantAvionesEnPermanencia >= 30)
            {
                nuevo = this.arrastrarVariablesEst(anterior);
                nuevo.evento = "Rechazo avión";
                nuevo.reloj = tiempoProximoEvento;

                if (anterior.tiempoProximaLlegada != nuevo.reloj)
                {
                    nuevo.tiempoProximaLlegada = anterior.tiempoProximaLlegada;
                }
                else
                {
                    // Calcular siguiente tiempo de llegada de prox avion
                    nuevo.rndLlegada = this.generator.NextRnd();
                    nuevo.tiempoEntreLlegadas = this.exponentialGenerator.Generate(nuevo.rndLlegada);
                    nuevo.tiempoProximaLlegada = nuevo.tiempoEntreLlegadas + nuevo.reloj;
                }

                // Arrastro todos los valores del vector de estado anterior
                nuevo.pista = new Pista();
                nuevo.pista.state = anterior.pista.state;
                nuevo.pista.colaEET = new Queue<Avion>(anterior.pista.colaEET);
                nuevo.pista.colaEEV = new Queue<Avion>(anterior.pista.colaEEV);
                nuevo.pista.tiempoRestanteAoD = anterior.pista.tiempoRestanteAoD;
                nuevo.pista.idClienteActual = anterior.pista.idClienteActual;

                nuevo.tiempoFinAterrizaje = anterior.tiempoFinAterrizaje;

                nuevo.tiempoFinDeDespegue = anterior.tiempoFinDeDespegue;

                nuevo.tiempoFinPermanencia = anterior.tiempoFinPermanencia;

                nuevo.tiempoFinPurga = anterior.tiempoFinPurga;
                nuevo.tiempoInestabilidad = anterior.tiempoInestabilidad;

                // Se recalculan variables estadísticas
                nuevo.porcAvionesAyDInst = (Convert.ToDouble(nuevo.cantAvionesAyDInst) / Convert.ToDouble(this.clientes.Count)) * 100;
                nuevo.avgEETTime = Convert.ToDouble(nuevo.acumEETTime) / Convert.ToDouble(this.clientes.Count);
                nuevo.avgEEVTime = Convert.ToDouble(nuevo.acumEEVTime) / Convert.ToDouble(this.clientes.Count);

                return nuevo;
            }

            Avion.count += 1;
            Avion avionNuevo = new Avion();

            nuevo = this.arrastrarVariablesEst(anterior);
            nuevo.evento = "Llegada Avion (" + Avion.count.ToString() + ")";
            nuevo.reloj = tiempoProximoEvento;

            // Se arrastran variables estadísticas.

            // Calcular siguiente tiempo de llegada de prox avion
            nuevo.rndLlegada = this.generator.NextRnd();
            nuevo.tiempoEntreLlegadas = this.exponentialGenerator.Generate(nuevo.rndLlegada);
            nuevo.tiempoProximaLlegada = nuevo.tiempoEntreLlegadas + nuevo.reloj;

            // Calculos variables de pista
            nuevo.pista = new Pista();
            nuevo.pista.state = anterior.pista.state;
            nuevo.pista.idClienteActual = anterior.pista.idClienteActual;
            nuevo.pista.tiempoRestanteAoD = anterior.pista.tiempoRestanteAoD;
            nuevo.pista.colaEEV = new Queue<Avion>(anterior.pista.colaEEV);
            nuevo.pista.colaEET = new Queue<Avion>(anterior.pista.colaEET);
            if (nuevo.pista.state == "Libre")
            {
                avionNuevo.estado = "EA";
                nuevo.rndAterrizaje = this.generator.NextRnd();
                nuevo.tiempoAterrizaje = this.uniformGeneratorAterrizaje.Generate(nuevo.rndAterrizaje);
                nuevo.tiempoFinAterrizaje = nuevo.tiempoAterrizaje + nuevo.reloj;
                avionNuevo.tiempoFinAterrizaje = nuevo.tiempoFinAterrizaje;
                nuevo.pista.state = "Ocupado";
                nuevo.pista.idClienteActual = avionNuevo.id;
                avionNuevo.instantLanding = true;

            }
            else
            {
                // Ver tema "Inestable"
                avionNuevo.estado = "EEV";
                nuevo.pista.colaEEV.Enqueue(avionNuevo);
                nuevo.tiempoFinAterrizaje = anterior.tiempoFinAterrizaje;
                avionNuevo.tiempoEEVin = nuevo.reloj;
            }

            // Calcular variables de despegue
            nuevo.tiempoFinDeDespegue = anterior.tiempoFinDeDespegue;

            // Inestabilidad
            nuevo.tiempoInestabilidad = anterior.tiempoInestabilidad;
            nuevo.tiempoFinPurga = anterior.tiempoFinPurga;

            // Clientes
            this.clientes.Add(avionNuevo);

            // Se recalculan variables estadísticas
            nuevo.porcAvionesAyDInst = (Convert.ToDouble(nuevo.cantAvionesAyDInst) / Convert.ToDouble(this.clientes.Count)) * 100;
            nuevo.avgEETTime = Convert.ToDouble(nuevo.acumEETTime) / Convert.ToDouble(this.clientes.Count);
            nuevo.avgEEVTime = Convert.ToDouble(nuevo.acumEEVTime) / Convert.ToDouble(this.clientes.Count);

            return nuevo;
        }

        private StateRow CrearStateRowFinDeDespegue(StateRow anterior, double tiempoProximoEvento, int avion)
        {
            StateRow nuevo = new StateRow();
            nuevo = this.arrastrarVariablesEst(anterior);
            nuevo.evento = "Fin Despegue (" + avion.ToString() + ")";
            nuevo.reloj = tiempoProximoEvento;


            nuevo.tiempoProximaLlegada = anterior.tiempoProximaLlegada;
            // Se arrastran variables estadísticas

            this.clientes[avion - 1].tiempoFinDeDespegue = 0;
            this.clientes[avion - 1].estado = "";
            //this.clientes[avion - 1].disabled = true;

            // Calculos variables de pista
            nuevo.pista = new Pista();
            nuevo.pista.state = anterior.pista.state;
            nuevo.pista.idClienteActual = anterior.pista.idClienteActual;
            nuevo.pista.tiempoRestanteAoD = anterior.pista.tiempoRestanteAoD;
            nuevo.pista.colaEEV = new Queue<Avion>(anterior.pista.colaEEV);
            nuevo.pista.colaEET = new Queue<Avion>(anterior.pista.colaEET);

            if (nuevo.pista.colaEEV.Count != 0)
            {
                // Calculos variables aterrizaje
                Avion avionNuevo = nuevo.pista.colaEEV.Dequeue();
                nuevo.rndAterrizaje = this.generator.NextRnd();
                nuevo.tiempoAterrizaje = this.uniformGeneratorAterrizaje.Generate(nuevo.rndAterrizaje);
                nuevo.tiempoFinAterrizaje = nuevo.tiempoAterrizaje + nuevo.reloj;
                nuevo.pista.state = "Ocupado";
                nuevo.pista.idClienteActual = avionNuevo.id;
                this.clientes[avionNuevo.id - 1].tiempoFinAterrizaje = nuevo.tiempoFinAterrizaje;
                this.clientes[avionNuevo.id - 1].estado = "EA";


                // Se chequea si el tiempo de espera en cola del avión desencolado es mayor al máx registrado,
                // de ser así lo asigna como maxEEVTime.
                // Puede que el chequeo no sea necesario
                if (this.clientes[avionNuevo.id - 1].tiempoEEVin != 0)
                {
                    double eevTime = nuevo.reloj - this.clientes[avionNuevo.id - 1].tiempoEEVin;
                    if (eevTime > nuevo.maxEEVTime) nuevo.maxEEVTime = eevTime;

                    nuevo.acumEEVTime += eevTime;
                }
            }
            else if (nuevo.pista.colaEET.Count != 0)
            {
                // Calculos variables de despegue
                Avion avionNuevo = nuevo.pista.colaEET.Dequeue();
                nuevo.rndDespegue = this.generator.NextRnd();
                nuevo.tiempoDeDespegue = this.uniformGeneratorDespegue.Generate(nuevo.rndDespegue);
                nuevo.tiempoFinDeDespegue = nuevo.tiempoDeDespegue + nuevo.reloj;
                nuevo.pista.state = "Ocupado";
                nuevo.pista.idClienteActual = avionNuevo.id;
                this.clientes[avionNuevo.id - 1].tiempoFinDeDespegue = nuevo.tiempoFinDeDespegue;
                this.clientes[avionNuevo.id - 1].estado = "ED";


                // Idem cola en vuelo
                if (this.clientes[avionNuevo.id - 1].tiempoEETin != 0)
                {
                    double eetTime = nuevo.reloj - this.clientes[avionNuevo.id - 1].tiempoEETin;
                    if (eetTime > nuevo.maxEETTime) nuevo.maxEETTime = eetTime;

                    nuevo.acumEETTime += eetTime;
                }
            }
            else
            {
                nuevo.pista.state = "Libre";
                nuevo.pista.idClienteActual = -1;
            }

            // Inestabilidad
            nuevo.tiempoInestabilidad = anterior.tiempoInestabilidad;
            nuevo.tiempoFinPurga = anterior.tiempoFinPurga;

            // Se recalculan variables estadísticas
            nuevo.porcAvionesAyDInst = (Convert.ToDouble(nuevo.cantAvionesAyDInst) / Convert.ToDouble(this.clientes.Count)) * 100;
            nuevo.avgEETTime = nuevo.acumEETTime / Convert.ToDouble(this.clientes.Count);
            nuevo.avgEEVTime = nuevo.acumEEVTime / Convert.ToDouble(this.clientes.Count);

            return nuevo;
        }

        private StateRow CrearStateRowFinAterrizaje(StateRow anterior, double tiempoProximoEvento, int avion)
        {
            StateRow nuevo = new StateRow();
            nuevo = this.arrastrarVariablesEst(anterior);
            nuevo.evento = "Fin Aterrizaje (" + avion.ToString() + ")";
            nuevo.reloj = tiempoProximoEvento;
            nuevo.tiempoProximaLlegada = anterior.tiempoProximaLlegada;
            // Se arrastran variables estadísticas


            //Calcular variables tiempo permanencia
            double ac = 0;
            nuevo.tiempoDePermanencia = convolutionGenerator.Generate(out ac);
            nuevo.rndPermanencia = ac;
            nuevo.tiempoFinPermanencia = nuevo.reloj + nuevo.tiempoDePermanencia;
            this.clientes[avion - 1].tiempoPermanencia = nuevo.tiempoFinPermanencia;
            this.clientes[avion - 1].tiempoFinAterrizaje = 0;
            this.clientes[avion - 1].estado = "EP";

            // Calculos variables de pista
            nuevo.pista = new Pista();
            nuevo.pista.state = anterior.pista.state;
            nuevo.pista.idClienteActual = anterior.pista.idClienteActual;
            nuevo.pista.tiempoRestanteAoD = anterior.pista.tiempoRestanteAoD;
            nuevo.pista.colaEEV = new Queue<Avion>(anterior.pista.colaEEV);
            nuevo.pista.colaEET = new Queue<Avion>(anterior.pista.colaEET);

            if (nuevo.pista.colaEEV.Count != 0)
            {
                // Calculos variables aterrizaje
                Avion avionNuevo = nuevo.pista.colaEEV.Dequeue();
                nuevo.rndAterrizaje = this.generator.NextRnd();
                nuevo.tiempoAterrizaje = this.uniformGeneratorAterrizaje.Generate(nuevo.rndAterrizaje);
                nuevo.tiempoFinAterrizaje = nuevo.tiempoAterrizaje + nuevo.reloj;
                nuevo.pista.state = "Ocupado";
                nuevo.pista.idClienteActual = avionNuevo.id;
                this.clientes[avionNuevo.id - 1].tiempoFinAterrizaje = nuevo.tiempoFinAterrizaje;
                this.clientes[avionNuevo.id - 1].estado = "EA";

                // Se chequea si el tiempo de espera en cola del avión desencolado es mayor al máx registrado,
                // de ser así lo asigna como maxEEVTime.
                if (this.clientes[avionNuevo.id - 1].tiempoEEVin != 0)
                {
                    double eevTime = nuevo.reloj - this.clientes[avionNuevo.id - 1].tiempoEEVin;
                    if (eevTime > nuevo.maxEEVTime) nuevo.maxEEVTime = eevTime;

                    nuevo.acumEEVTime += eevTime;
                }
            }
            else if (nuevo.pista.colaEET.Count != 0)
            {
                // Calculos variables de despegue
                Avion avionNuevo = nuevo.pista.colaEET.Dequeue();
                nuevo.rndDespegue = this.generator.NextRnd();
                nuevo.tiempoDeDespegue = this.uniformGeneratorDespegue.Generate(nuevo.rndDespegue);
                nuevo.tiempoFinDeDespegue = nuevo.tiempoDeDespegue + nuevo.reloj;
                nuevo.pista.state = "Ocupado";
                nuevo.pista.idClienteActual = avionNuevo.id;
                this.clientes[avionNuevo.id - 1].tiempoFinDeDespegue = nuevo.tiempoFinDeDespegue;
                this.clientes[avionNuevo.id - 1].estado = "ED";

                if (this.clientes[avionNuevo.id - 1].tiempoEETin != 0)
                {
                    double eetTime = nuevo.reloj - this.clientes[avionNuevo.id - 1].tiempoEETin;
                    if (eetTime > nuevo.maxEETTime) nuevo.maxEETTime = eetTime;

                    nuevo.acumEETTime += eetTime;
                }
            }
            else
            {
                nuevo.pista.state = "Libre";
                nuevo.pista.idClienteActual = -1;
            }

            // Inestabilidad
            nuevo.tiempoInestabilidad = anterior.tiempoInestabilidad;
            nuevo.tiempoFinPurga = anterior.tiempoFinPurga;

            // Se recalculan variables estadísticas
            nuevo.porcAvionesAyDInst = (Convert.ToDouble(nuevo.cantAvionesAyDInst) / Convert.ToDouble(this.clientes.Count)) * 100;
            nuevo.avgEETTime = Convert.ToDouble(nuevo.acumEETTime) / Convert.ToDouble(this.clientes.Count);
            nuevo.avgEEVTime = Convert.ToDouble(nuevo.acumEEVTime) / Convert.ToDouble(this.clientes.Count);

            return nuevo;
        }

        private StateRow CrearStateRowFinDePermanencia(StateRow anterior, double tiempoProximoEvento, int avion)
        {
            StateRow nuevo = new StateRow();
            nuevo = this.arrastrarVariablesEst(anterior);
            nuevo.evento = "Fin permanencia (" + avion.ToString() + ")";
            nuevo.reloj = tiempoProximoEvento;


            // Calcular siguiente tiempo de llegada de prox avion
            nuevo.tiempoProximaLlegada = anterior.tiempoProximaLlegada;

            // Calcular variables de aterrizaje
            nuevo.tiempoFinAterrizaje = anterior.tiempoFinAterrizaje;

            nuevo.tiempoFinDeDespegue = anterior.tiempoFinDeDespegue;

            // Calculos variables de pista
            nuevo.pista = new Pista();
            nuevo.pista.state = anterior.pista.state;
            nuevo.pista.idClienteActual = anterior.pista.idClienteActual;
            nuevo.pista.tiempoRestanteAoD = anterior.pista.tiempoRestanteAoD;
            nuevo.pista.colaEEV = new Queue<Avion>(anterior.pista.colaEEV);
            nuevo.pista.colaEET = new Queue<Avion>(anterior.pista.colaEET);


            if (nuevo.pista.state == "Libre")
            {
                // Calcular variables de despegue
                this.clientes[avion - 1].estado = "ED";
                nuevo.rndDespegue = this.generator.NextRnd();
                nuevo.tiempoDeDespegue = this.uniformGeneratorDespegue.Generate(nuevo.rndDespegue);
                nuevo.tiempoFinDeDespegue = nuevo.tiempoDeDespegue + nuevo.reloj;
                this.clientes[avion - 1].tiempoFinDeDespegue = nuevo.tiempoFinDeDespegue;
                nuevo.pista.state = "Ocupado";
                nuevo.pista.idClienteActual = avion;

                if (this.clientes[avion - 1].instantLanding) nuevo.cantAvionesAyDInst++;

            }
            else
            {
                this.clientes[avion - 1].estado = "EET";
                nuevo.pista.colaEET.Enqueue(this.clientes[avion - 1]);
                this.clientes[avion - 1].tiempoEETin = nuevo.reloj;
            }
            this.clientes[avion - 1].tiempoPermanencia = 0;

            // Inestabilidad
            nuevo.tiempoInestabilidad = anterior.tiempoInestabilidad;
            nuevo.tiempoFinPurga = anterior.tiempoFinPurga;

            // Se recalculan variables estadísticas
            nuevo.porcAvionesAyDInst = (Convert.ToDouble(nuevo.cantAvionesAyDInst) / Convert.ToDouble(this.clientes.Count)) * 100;

            nuevo.avgEETTime = Convert.ToDouble(nuevo.acumEETTime) / Convert.ToDouble(this.clientes.Count);
            nuevo.avgEEVTime = Convert.ToDouble(nuevo.acumEEVTime) / Convert.ToDouble(this.clientes.Count);

            return nuevo;
        }

        private StateRow CrearStateRowInicioInestabilidad(StateRow anterior, double tiempoProximoEvento)
        {
            StateRow nuevo = new StateRow();
            nuevo = this.arrastrarVariablesEst(anterior);
            nuevo.evento = "Inicio Inestabilidad";
            nuevo.reloj = tiempoProximoEvento;
            nuevo.pista = new Pista();

            // Calcular siguiente tiempo de llegada de prox avion
            nuevo.tiempoProximaLlegada = anterior.tiempoProximaLlegada;

            // Calcular variables de aterrizaje
            if (anterior.tiempoFinAterrizaje != 0)
            {
                nuevo.tiempoFinAterrizaje = 0;
                this.clientes[anterior.pista.idClienteActual - 1].estado = "IA";
                this.clientes[anterior.pista.idClienteActual - 1].tiempoFinAterrizaje = 0;
                nuevo.pista.tiempoRestanteAoD = anterior.tiempoFinAterrizaje - nuevo.reloj;
            }
            // Calcular variables de despegue
            else if (anterior.tiempoFinDeDespegue != 0)
            {
                nuevo.tiempoFinDeDespegue = 0;
                this.clientes[anterior.pista.idClienteActual - 1].estado = "ID";
                this.clientes[anterior.pista.idClienteActual - 1].tiempoFinDeDespegue = 0;
                nuevo.pista.tiempoRestanteAoD = anterior.tiempoFinDeDespegue - nuevo.reloj;
            }

            // Calculos variables de pista
            nuevo.pista.state = "Inestable";
            nuevo.pista.idClienteActual = anterior.pista.idClienteActual;
            nuevo.pista.colaEEV = new Queue<Avion>(anterior.pista.colaEEV);
            nuevo.pista.colaEET = new Queue<Avion>(anterior.pista.colaEET);

            // Calculos purga
            nuevo.tiempoFinPurga = nuevo.reloj + 20.0000;

            // Se recalculan variables estadísticas
            nuevo.porcAvionesAyDInst = (Convert.ToDouble(nuevo.cantAvionesAyDInst) / Convert.ToDouble(this.clientes.Count)) * 100;

            nuevo.avgEETTime = Convert.ToDouble(nuevo.acumEETTime) / Convert.ToDouble(this.clientes.Count);
            nuevo.avgEEVTime = Convert.ToDouble(nuevo.acumEEVTime) / Convert.ToDouble(this.clientes.Count);

            return nuevo;
        }

        private StateRow CrearStateRowFinPurga(StateRow anterior, double tiempoProximoEvento)
        {
            StateRow nuevo = new StateRow();
            nuevo = this.arrastrarVariablesEst(anterior);
            nuevo.evento = "Fin Purga";
            nuevo.reloj = tiempoProximoEvento;

            // Calculos variables de pista
            nuevo.pista = new Pista();
            nuevo.pista.colaEEV = new Queue<Avion>(anterior.pista.colaEEV);
            nuevo.pista.colaEET = new Queue<Avion>(anterior.pista.colaEET);
            nuevo.pista.idClienteActual = anterior.pista.idClienteActual;


            // Calcular siguiente tiempo de llegada de prox avion
            nuevo.tiempoProximaLlegada = anterior.tiempoProximaLlegada;


            // Calcular variables de aterrizaje
            if (anterior.pista.idClienteActual != -1)
            {
                if (this.clientes[anterior.pista.idClienteActual - 1].estado == "IA")
                {
                    // Calcular variables de aterrizaje
                    nuevo.tiempoFinAterrizaje = anterior.pista.tiempoRestanteAoD + nuevo.reloj;
                    this.clientes[anterior.pista.idClienteActual - 1].estado = "EA";
                    this.clientes[anterior.pista.idClienteActual - 1].tiempoFinAterrizaje = anterior.pista.tiempoRestanteAoD + nuevo.reloj;
                }
                else
                {
                    // Calcular variables de despegue
                    nuevo.tiempoFinDeDespegue = anterior.pista.tiempoRestanteAoD + nuevo.reloj;
                    this.clientes[anterior.pista.idClienteActual - 1].estado = "ED";
                    this.clientes[anterior.pista.idClienteActual - 1].tiempoFinDeDespegue = anterior.pista.tiempoRestanteAoD + nuevo.reloj;
                }
                nuevo.pista.state = "Ocupado";
            }
            else if (nuevo.pista.colaEEV.Count != 0)
            {
                // Calculos variables aterrizaje
                Avion avionNuevo = nuevo.pista.colaEEV.Dequeue();
                nuevo.rndAterrizaje = this.generator.NextRnd();
                nuevo.tiempoAterrizaje = this.uniformGeneratorAterrizaje.Generate(nuevo.rndAterrizaje);
                nuevo.tiempoFinAterrizaje = nuevo.tiempoAterrizaje + nuevo.reloj;
                nuevo.pista.state = "Ocupado";
                nuevo.pista.idClienteActual = avionNuevo.id;
                this.clientes[avionNuevo.id - 1].tiempoFinAterrizaje = nuevo.tiempoFinAterrizaje;
                this.clientes[avionNuevo.id - 1].estado = "EA";

                // Se chequea si el tiempo de espera en cola del avión desencolado es mayor al máx registrado,
                // de ser así lo asigna como maxEEVTime.
                if (this.clientes[avionNuevo.id - 1].tiempoEEVin != 0)
                {
                    double eevTime = nuevo.reloj - this.clientes[avionNuevo.id - 1].tiempoEEVin;
                    if (eevTime > nuevo.maxEEVTime) nuevo.maxEEVTime = eevTime;

                    nuevo.acumEEVTime += eevTime;
                }
            }
            else if (nuevo.pista.colaEET.Count != 0)
            {
                // Calculos variables de despegue
                Avion avionNuevo = nuevo.pista.colaEET.Dequeue();
                nuevo.rndDespegue = this.generator.NextRnd();
                nuevo.tiempoDeDespegue = this.uniformGeneratorDespegue.Generate(nuevo.rndDespegue);
                nuevo.tiempoFinDeDespegue = nuevo.tiempoDeDespegue + nuevo.reloj;
                nuevo.pista.state = "Ocupado";
                nuevo.pista.idClienteActual = avionNuevo.id;
                this.clientes[avionNuevo.id - 1].tiempoFinDeDespegue = nuevo.tiempoFinDeDespegue;
                this.clientes[avionNuevo.id - 1].estado = "ED";

                if (this.clientes[avionNuevo.id - 1].tiempoEETin != 0)
                {
                    double eetTime = nuevo.reloj - this.clientes[avionNuevo.id - 1].tiempoEETin;
                    if (eetTime > nuevo.maxEETTime) nuevo.maxEETTime = eetTime;

                    nuevo.acumEETTime += eetTime;
                }
            }
            else
            {
                nuevo.pista.state = "Libre";
                nuevo.pista.idClienteActual = -1;
            }

            // Calculos nuevo evento inestabilidad
            nuevo.rndInestable = generator.NextRnd();
            if (nuevo.rndInestable < 0.2)
            {
                nuevo.tiempoInestabilidad = nuevo.reloj + 320.7;
            }
            else if (nuevo.rndInestable < 0.5)
            {
                nuevo.tiempoInestabilidad = nuevo.reloj + 367.5;
            }
            else nuevo.tiempoInestabilidad = nuevo.reloj + 417.2;


            return nuevo;
        }

        private StateRow arrastrarVariablesEst(StateRow _anterior)
        {
            StateRow nuevo = new StateRow();

            nuevo.maxEEVTime = _anterior.maxEEVTime;
            nuevo.maxEETTime = _anterior.maxEETTime;
            nuevo.porcAvionesAyDInst = _anterior.porcAvionesAyDInst;
            nuevo.cantAvionesAyDInst = _anterior.cantAvionesAyDInst;
            nuevo.acumEETTime = _anterior.acumEETTime;
            nuevo.acumEEVTime = _anterior.acumEEVTime;
            nuevo.avgEETTime = _anterior.avgEETTime;
            nuevo.avgEEVTime = _anterior.avgEEVTime;

            return nuevo;
        }
    }

}

