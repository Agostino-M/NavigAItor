using UnityEngine;

public class KalmanFilter
{
    private Matrix4x4 x; // Stato attuale (posizione e velocità in 3D)
    private Matrix4x4 A; // Matrice di transizione di stato
    private Matrix4x4 S; // Covarianza iniziale
    private Matrix4x4 C; // Matrice di osservazione
    private Matrix4x4 R; // Rumore di processo
    private Matrix4x4 Q; // Rumore di misura

    public KalmanFilter(Vector3 position)
    {
        // Inizializzazione dello stato con posizione e velocità
        x = Matrix4x4.zero;
        x.SetColumn(0, new Vector4(position.x, position.y, position.z, 0)); // Posizione
        x.SetColumn(1, new Vector4(0, 0, 0, 0)); // Velocità

        // Matrice di transizione di stato (4x4 per gestire posizione e velocità in 3D)
        A = new Matrix4x4(
            new Vector4(1, 0, 0, Time.deltaTime),
            new Vector4(0, 1, 0, Time.deltaTime),
            new Vector4(0, 0, 1, Time.deltaTime),
            new Vector4(0, 0, 0, 1)
        );

        // Inizializzazione delle matrici di covarianza, osservazione e rumore
        S = Matrix4x4.identity;
        C = Matrix4x4.identity;

        // Rumore di processo (regolalo in base alla dinamica del drone)
        R = MultiplyMatrixByFloat(Matrix4x4.identity, 0.1f); // Valore "alto" perchè il drone effettua spostamenti rapidi e per non perdere la precisione equilibrando con il rumore di misura

        // Rumore di misura (regolalo in base alla precisione dei sensori)
        Q = Matrix4x4.zero;
        Q.m00 = 0.25f; // Varianza del rumore su x (0.5^2 = 0.25)
        Q.m11 = 0.25f; // Varianza del rumore su z (0.5^2 = 0.25)
        Q.m22 = 0.01f; // Varianza del rumore su y (supponiamo meno rumore sull'asse y)
    }

    public Vector3 UpdatePosition(Vector3 position)
    {
        // Aggiorna la matrice di transizione di stato con il nuovo deltaTime
        A = new Matrix4x4(
            new Vector4(1, 0, 0, Time.deltaTime),
            new Vector4(0, 1, 0, Time.deltaTime),
            new Vector4(0, 0, 1, Time.deltaTime),
            new Vector4(0, 0, 0, 1)
        );

        Predict();
        Correct(position);
        return new Vector3(x.m00, x.m10, x.m20); // Restituisce la posizione filtrata
    }

    private void Predict()
    {
        // Predizione dello stato: x = A * x
        x = A * x;

        // Predizione della covarianza: S = A * S * A^T + R
        S = SumMatrix4x4(A * S * A.transpose, R);
    }

    private void Correct(Vector3 position)
    {
        // Misurazione della posizione
        Matrix4x4 z = Matrix4x4.zero;
        z.SetColumn(0, new Vector4(position.x, position.y, position.z, 0));

        // Calcolo del guadagno di Kalman: K = S * C^T * (C * S * C^T + Q)^-1
        Matrix4x4 K = S * C.transpose * InverseMatrix4x4(SumMatrix4x4(C * S * C.transpose, Q));

        // Correzione dello stato: x = x + K * (z - C * x)
        x = SumMatrix4x4(x, K * SubMatrix4x4(z, InverseMatrix4x4(C) * x));

        // Correzione della covarianza: S = (I - K * C) * S
        S = SubMatrix4x4(S, K * C * S);
    }

    private Matrix4x4 SumMatrix4x4(Matrix4x4 m1, Matrix4x4 m2)
    {
        Matrix4x4 sum = Matrix4x4.zero;
        for (int i = 0; i < 4; i++)
        {
            for (int j = 0; j < 4; j++)
            {
                sum[i, j] = m1[i, j] + m2[i, j];
            }
        }
        return sum;
    }

    private Matrix4x4 SubMatrix4x4(Matrix4x4 m1, Matrix4x4 m2)
    {
        Matrix4x4 sub = Matrix4x4.zero;
        for (int i = 0; i < 4; i++)
        {
            for (int j = 0; j < 4; j++)
            {
                sub[i, j] = m1[i, j] - m2[i, j];
            }
        }
        return sub;
    }

    private Matrix4x4 MultiplyMatrixByFloat(Matrix4x4 matrix, float scalar)
    {
        Matrix4x4 result = Matrix4x4.zero;
        for (int i = 0; i < 4; i++)
        {
            for (int j = 0; j < 4; j++)
            {
                result[i, j] = matrix[i, j] * scalar;
            }
        }
        return result;
    }

    private Matrix4x4 InverseMatrix4x4(Matrix4x4 m)
    {
        // Implementazione dell'inversione di una matrice 4x4 (richiede una libreria esterna o un'implementazione manuale)
        // Placeholder: restituisce la matrice identità (da sostituire con l'implementazione corretta)
        return Matrix4x4.identity;
    }
}