using System;
using System.Collections.Generic;
using System.IO;
using ZOSAPI;
using ZOSAPI.Analysis;
using ZOSAPI.Analysis.Data;
using ZOSAPI.Editors.MFE;
using ZOSAPI.Editors.NCE;
using ZOSAPI.Tools.RayTrace;

namespace CSharpUserOperandApplication
{
    class Program
    {
        private static INSCRayTrace ray_trace;
        private static INCERow detector;
        private static IA_ detector_viewer;
        private static IAR_ detector_results;

        static void Main(string[] args)
        {
            // Find the installed version of OpticStudio
            bool isInitialized = ZOSAPI_NetHelper.ZOSAPI_Initializer.Initialize();
            // Note -- uncomment the following line to use a custom initialization path
            //bool isInitialized = ZOSAPI_NetHelper.ZOSAPI_Initializer.Initialize(@"C:\Program Files\OpticStudio\");
            if (isInitialized)
            {
                LogInfo("Found OpticStudio at: " + ZOSAPI_NetHelper.ZOSAPI_Initializer.GetZemaxDirectory());
            }
            else
            {
                HandleError("Failed to locate OpticStudio!");
                return;
            }

            BeginUserOperand();
        }

        static void BeginUserOperand()
        {
            // Create the initial connection class
            ZOSAPI_Connection TheConnection = new ZOSAPI_Connection();

            // Attempt to connect to the existing OpticStudio instance
            IZOSAPI_Application TheApplication = null;
            try
            {
                TheApplication = TheConnection.ConnectToApplication(); // this will throw an exception if not launched from OpticStudio
            }
            catch (Exception ex)
            {
                HandleError(ex.Message);
                return;
            }
            if (TheApplication == null)
            {
                HandleError("An unknown connection error occurred!");
                return;
            }

            // Check the connection status
            if (!TheApplication.IsValidLicenseForAPI)
            {
                HandleError("Failed to connect to OpticStudio: " + TheApplication.LicenseStatus);
                return;
            }
            if (TheApplication.Mode != ZOSAPI_Mode.Operand)
            {
                HandleError("User plugin was started in the wrong mode: expected Operand, found " + TheApplication.Mode.ToString());
                return;
            }

            // Read the operand arguments
            int detector_number = (int) TheApplication.OperandArgument1; // Corresponds to Hx column
            double Hy = TheApplication.OperandArgument2; // Unused
            double Px = TheApplication.OperandArgument3; // Unused
            double Py = TheApplication.OperandArgument4; // Unused

            // Initialize the output array
            int maxResultLength = TheApplication.OperandResults.Length;
            double[] operandResults = new double[maxResultLength];

            IOpticalSystem TheSystem = TheApplication.PrimarySystem;
            // Add your custom code here...

            // Find detector size and number of pixels
            detector = TheSystem.NCE.GetObjectAt(detector_number);

            double x_hw = detector.GetObjectCell(ZOSAPI.Editors.NCE.ObjectColumn.Par1).DoubleValue;
            double y_hw = detector.GetObjectCell(ZOSAPI.Editors.NCE.ObjectColumn.Par2).DoubleValue;
            int x_pix = detector.GetObjectCell(ZOSAPI.Editors.NCE.ObjectColumn.Par3).IntegerValue;
            int y_pix = detector.GetObjectCell(ZOSAPI.Editors.NCE.ObjectColumn.Par4).IntegerValue;
            int x_centre = (int)Math.Round((x_pix - 1) / 2.0);
            int y_centre = (int)Math.Round((y_pix - 1) / 2.0);
            double x_delta = x_hw / x_centre;
            double y_delta = y_hw / y_centre;

            // Find centroid location
            double x_loc, y_loc;
            
            TheSystem.NCE.GetDetectorData(detector_number, -6, 1, out x_loc);
            TheSystem.NCE.GetDetectorData(detector_number, -7, 1, out y_loc);

            // Calculate index of centroid row and column approximately (nearest-neighbor)
            int x_index = (int)( x_loc / x_hw * (double)x_centre ) + x_centre;
            int y_index = (int)( y_loc / y_hw * (double)y_centre ) + y_centre;

            // Retrieve all detector data
            double[,] detector_data = TheSystem.NCE.GetAllDetectorDataSafe(detector_number, 1);

            // Extract centroid row and column
            double[] col = new double[y_pix];
            double[] row = new double[x_pix];

            for (int yy = 0; yy < y_pix; yy++)
                col[yy] = detector_data[yy, x_index];

            for (int xx = 0; xx < x_pix; xx++)
                row[xx] = detector_data[y_index, xx];

            // Return FWHM, Sigma (standard deviation), and centroid locations
            operandResults[0] = CalcFWHM(col) * y_delta;
            operandResults[1] = CalcFWHM(row) * x_delta;
            operandResults[2] = CalcSigma(col) * y_delta;
            operandResults[3] = CalcSigma(row) * x_delta;
            operandResults[4] = x_loc;
            operandResults[5] = y_loc;

            // Clean up
            FinishUserOperand(TheApplication, operandResults);
        }

        // Calculates the mean index in a row or column (in index units!)
        static double CalcMean(double[] data)
        {
            double sum = 0.0;
            double sum_weights = 0.0;

            for (int index = 0; index < data.Length; index++)
            {
                sum += index * data[index];
                sum_weights += data[index];
            }

            return sum / sum_weights;
        }

        // Calculates the standard deviation in a row or column (in index units!)
        static double CalcSigma(double[] data)
        {
            double sum = 0.0;
            double sum_weights = 0.0;

            double mean = CalcMean(data);

            for (int index = 0; index < data.Length; index++)
            {
                sum += data[index] * (index - mean) * (index - mean);
                sum_weights += data[index];
            }

            return Math.Sqrt( sum / ( ( data.Length - 1.0 ) / data.Length * sum_weights ) );
        }

        // Calculates the FWHM based on the standard deviation assuming a normal distribution
        static double CalcFWHM(double[] data)
        {
            double sigma = CalcSigma(data);

            return 2.355 * sigma;
        }

        static void FinishUserOperand(IZOSAPI_Application TheApplication, double[] resultData)
        {
            // Note - OpticStudio will wait for the operand to complete until this application exits 
            if (TheApplication != null)
            {
                TheApplication.OperandResults.WriteData(resultData.Length, resultData);
            }
        }

        static void LogInfo(string message)
        {
            // TODO - add custom logging
            Console.WriteLine(message);
        }

        static void HandleError(string errorMessage)
        {
            // TODO - add custom error handling
            throw new Exception(errorMessage);
        }

    }
}
