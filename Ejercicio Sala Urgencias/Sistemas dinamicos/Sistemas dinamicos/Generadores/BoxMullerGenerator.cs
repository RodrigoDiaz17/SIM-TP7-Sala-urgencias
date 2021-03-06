﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GeneradorDeNumerosAleatorios;

namespace RandomVarGenerator
{
    class BoxMullerGenerator
    {
        public Generator congruentialGenerator = new Generator() { seed = 31767, a = 71561, c = 56822, M = 341157 };
        double[] resultPair = new double[2];
        public double stDeviation { get; set; }
        public double mean { get; set; }

        public double Generate(double n1, double n2)
        {

            double r1 = ((Math.Sqrt(-2 * Math.Log(n1))) * Math.Cos(2 * Math.PI * n2)) * this.stDeviation + this.mean;

            return r1;
        }
    }
}

