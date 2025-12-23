"""
ML-предиктор производительности компьютерных систем
"""

import numpy as np
import pandas as pd
from sklearn.ensemble import RandomForestRegressor, IsolationForest
from sklearn.linear_model import LinearRegression
from sklearn.model_selection import train_test_split, cross_val_score
from sklearn.preprocessing import StandardScaler
from sklearn.metrics import mean_squared_error, r2_score, mean_absolute_error
import matplotlib.pyplot as plt
import seaborn as sns
import pickle
from typing import Dict, List, Tuple, Optional
import warnings
warnings.filterwarnings('ignore')


class PerformancePredictor:
    """Предсказатель производительности на основе ML"""
    
    def __init__(self, model_type: str = 'random_forest'):
        """
        Args:
            model_type: Тип модели ('random_forest', 'linear', 'gradient_boosting')
        """
        self.model_type = model_type
        self.model = None
        self.scaler = StandardScaler()
        self.is_trained = False
        self.feature_names = None
        self.feature_importance = None
        
        self._init_model()
    
    def _init_model(self):
        """Инициализация модели"""
        if self.model_type == 'random_forest':
            self.model = RandomForestRegressor(
                n_estimators=100,
                max_depth=20,
                min_samples_split=5,
                random_state=42,
                n_jobs=-1
            )
        elif self.model_type == 'linear':
            self.model = LinearRegression()
        elif self.model_type == 'gradient_boosting':
            from sklearn.ensemble import GradientBoostingRegressor
            self.model = GradientBoostingRegressor(
                n_estimators=100,
                max_depth=5,
                learning_rate=0.1,
                random_state=42
            )
    
    def train(self, X: np.ndarray, y: np.ndarray, 
              feature_names: Optional[List[str]] = None) -> Dict:
        """
        Обучение модели
        
        Args:
            X: Матрица признаков (N x M)
            y: Вектор целевых значений (N,)
            feature_names: Названия признаков
            
        Returns:
            Словарь с метриками обучения
        """
        self.feature_names = feature_names or [f'feature_{i}' for i in range(X.shape[1])]
        
        # Разделение на train/test
        X_train, X_test, y_train, y_test = train_test_split(
            X, y, test_size=0.2, random_state=42
        )
        
        # Нормализация
        X_train_scaled = self.scaler.fit_transform(X_train)
        X_test_scaled = self.scaler.transform(X_test)
        
        # Обучение
        self.model.fit(X_train_scaled, y_train)
        self.is_trained = True
        
        # Предсказания
        y_train_pred = self.model.predict(X_train_scaled)
        y_test_pred = self.model.predict(X_test_scaled)
        
        # Метрики
        metrics = {
            'train_r2': r2_score(y_train, y_train_pred),
            'test_r2': r2_score(y_test, y_test_pred),
            'train_mse': mean_squared_error(y_train, y_train_pred),
            'test_mse': mean_squared_error(y_test, y_test_pred),
            'train_mae': mean_absolute_error(y_train, y_train_pred),
            'test_mae': mean_absolute_error(y_test, y_test_pred)
        }
        
        # Feature importance (для Random Forest)
        if hasattr(self.model, 'feature_importances_'):
            self.feature_importance = dict(zip(
                self.feature_names,
                self.model.feature_importances_
            ))
        
        # Cross-validation
        cv_scores = cross_val_score(
            self.model, X_train_scaled, y_train, cv=5, scoring='r2'
        )
        metrics['cv_mean_r2'] = cv_scores.mean()
        metrics['cv_std_r2'] = cv_scores.std()
        
        return metrics
    
    def predict(self, features: Dict[str, float]) -> float:
        """
        Предсказание производительности
        
        Args:
            features: Словарь с характеристиками системы
            
        Returns:
            Предсказанный score производительности
        """
        if not self.is_trained:
            raise ValueError("Model is not trained")
        
        # Преобразуем словарь в numpy array
        X = np.array([[features[name] for name in self.feature_names]])
        X_scaled = self.scaler.transform(X)
        
        return float(self.model.predict(X_scaled)[0])
    
    def predict_batch(self, X: np.ndarray) -> np.ndarray:
        """Пакетное предсказание"""
        if not self.is_trained:
            raise ValueError("Model is not trained")
        
        X_scaled = self.scaler.transform(X)
        return self.model.predict(X_scaled)
    
    def save(self, filepath: str):
        """Сохранение модели"""
        model_data = {
            'model': self.model,
            'scaler': self.scaler,
            'feature_names': self.feature_names,
            'feature_importance': self.feature_importance,
            'model_type': self.model_type
        }
        
        with open(filepath, 'wb') as f:
            pickle.dump(model_data, f)
        
        print(f"Model saved to: {filepath}")
    
    @classmethod
    def load(cls, filepath: str) -> 'PerformancePredictor':
        """Загрузка модели"""
        with open(filepath, 'rb') as f:
            model_data = pickle.load(f)
        
        predictor = cls(model_type=model_data['model_type'])
        predictor.model = model_data['model']
        predictor.scaler = model_data['scaler']
        predictor.feature_names = model_data['feature_names']
        predictor.feature_importance = model_data['feature_importance']
        predictor.is_trained = True
        
        return predictor
    
    def plot_feature_importance(self, save_path: Optional[str] = None):
        """Визуализация важности признаков"""
        if not self.feature_importance:
            print("Feature importance not available for this model type")
            return
        
        sorted_features = sorted(
            self.feature_importance.items(),
            key=lambda x: x[1],
            reverse=True
        )
        
        features, importance = zip(*sorted_features)
        
        plt.figure(figsize=(10, 6))
        plt.barh(features, importance)
        plt.xlabel('Importance')
        plt.title('Feature Importance')
        plt.tight_layout()
        
        if save_path:
            plt.savefig(save_path, dpi=150)
        
        plt.show()


class AnomalyDetector:
    """Детектор аномалий в работе системы"""
    
    def __init__(self, contamination: float = 0.1):
        """
        Args:
            contamination: Ожидаемая доля аномалий в данных
        """
        self.model = IsolationForest(
            contamination=contamination,
            random_state=42,
            n_estimators=100
        )
        self.is_trained = False
        self.scaler = StandardScaler()
    
    def train(self, X: np.ndarray):
        """Обучение детектора"""
        X_scaled = self.scaler.fit_transform(X)
        self.model.fit(X_scaled)
        self.is_trained = True
    
    def detect(self, X: np.ndarray) -> np.ndarray:
        """
        Детекция аномалий
        
        Returns:
            Массив меток: 1 = нормальный, -1 = аномалия
        """
        if not self.is_trained:
            raise ValueError("Model is not trained")
        
        X_scaled = self.scaler.transform(X)
        return self.model.predict(X_scaled)
    
    def get_anomaly_score(self, X: np.ndarray) -> np.ndarray:
        """
        Получение score аномальности
        
        Returns:
            Массив scores (меньше = более аномально)
        """
        if not self.is_trained:
            raise ValueError("Model is not trained")
        
        X_scaled = self.scaler.transform(X)
        return self.model.decision_function(X_scaled)


def generate_synthetic_dataset(n_samples: int = 1000) -> Tuple[pd.DataFrame, np.ndarray]:
    """
    Генерация синтетического датасета для обучения
    
    Returns:
        (features_df, performance_scores)
    """
    np.random.seed(42)
    
    # Генерация признаков
    data = {
        'cpu_cores': np.random.randint(4, 32, n_samples),
        'cpu_freq_mhz': np.random.randint(2000, 5000, n_samples),
        'cpu_cache_mb': np.random.choice([8, 16, 32, 64], n_samples),
        'ram_gb': np.random.choice([8, 16, 32, 64, 128], n_samples),
        'ram_freq_mhz': np.random.choice([2400, 3200, 3600, 4800, 6000], n_samples),
        'disk_type': np.random.choice([0, 1, 2], n_samples),  # 0=HDD, 1=SATA SSD, 2=NVMe
        'disk_speed_mbps': np.random.randint(100, 7000, n_samples),
        'gpu_cores': np.random.randint(0, 10240, n_samples),
        'gpu_memory_gb': np.random.choice([0, 4, 8, 12, 16, 24], n_samples)
    }
    
    df = pd.DataFrame(data)
    
    # Генерация целевой переменной (производительность)
    # Формула: комбинация факторов с некоторым шумом
    performance = (
        df['cpu_cores'] * 150 +
        df['cpu_freq_mhz'] * 0.05 +
        df['cpu_cache_mb'] * 20 +
        df['ram_gb'] * 50 +
        df['ram_freq_mhz'] * 0.02 +
        df['disk_type'] * 500 +
        df['disk_speed_mbps'] * 0.1 +
        df['gpu_cores'] * 0.5 +
        df['gpu_memory_gb'] * 100 +
        np.random.normal(0, 500, n_samples)  # Шум
    )
    
    # Нормализация к диапазону 0-10000
    performance = (performance - performance.min()) / (performance.max() - performance.min()) * 10000
    
    return df, performance.values


def roofline_model(peak_performance_gflops: float,
                   memory_bandwidth_gbs: float,
                   operational_intensity: float) -> float:
    """
    Roofline модель для оценки потолка производительности
    
    Args:
        peak_performance_gflops: Пиковая производительность (GFLOPS)
        memory_bandwidth_gbs: Пропускная способность памяти (GB/s)
        operational_intensity: Операции/байт (FLOP/Byte)
        
    Returns:
        Достижимая производительность (GFLOPS)
    """
    # Memory-bound: performance = bandwidth * intensity
    memory_bound = memory_bandwidth_gbs * operational_intensity
    
    # Compute-bound: performance = peak_performance
    compute_bound = peak_performance_gflops
    
    # Фактическая производительность = минимум из двух
    return min(memory_bound, compute_bound)


if __name__ == "__main__":
    print("═══════════════════════════════════════════════════════")
    print("       ML Performance Predictor (Stage 6)")
    print("═══════════════════════════════════════════════════════\n")
    
    # 1. Генерация датасета
    print("1. Generating synthetic dataset...")
    df, performance = generate_synthetic_dataset(n_samples=2000)
    print(f"   Dataset size: {len(df)} samples")
    print(f"   Features: {list(df.columns)}\n")
    
    # 2. Обучение модели
    print("2. Training performance predictor...")
    predictor = PerformancePredictor(model_type='random_forest')
    
    metrics = predictor.train(
        X=df.values,
        y=performance,
        feature_names=list(df.columns)
    )
    
    print(f"   Training R²: {metrics['train_r2']:.4f}")
    print(f"   Test R²: {metrics['test_r2']:.4f}")
    print(f"   Test MAE: {metrics['test_mae']:.2f}")
    print(f"   CV R² (5-fold): {metrics['cv_mean_r2']:.4f} ± {metrics['cv_std_r2']:.4f}\n")
    
    # 3. Feature importance
    print("3. Feature importance:")
    for feature, importance in sorted(
        predictor.feature_importance.items(),
        key=lambda x: x[1],
        reverse=True
    ):
        print(f"   {feature:20s}: {importance:.4f}")
    
    predictor.plot_feature_importance('feature_importance.png')
    
    # 4. Предсказание для новой системы
    print("\n4. Predicting performance for sample configurations...\n")
    
    test_configs = [
        {
            'name': 'Budget Gaming PC',
            'features': {
                'cpu_cores': 6, 'cpu_freq_mhz': 3600, 'cpu_cache_mb': 16,
                'ram_gb': 16, 'ram_freq_mhz': 3200,
                'disk_type': 2, 'disk_speed_mbps': 3500,
                'gpu_cores': 2560, 'gpu_memory_gb': 8
            }
        },
        {
            'name': 'High-End Workstation',
            'features': {
                'cpu_cores': 16, 'cpu_freq_mhz': 4200, 'cpu_cache_mb': 64,
                'ram_gb': 64, 'ram_freq_mhz': 4800,
                'disk_type': 2, 'disk_speed_mbps': 7000,
                'gpu_cores': 10240, 'gpu_memory_gb': 24
            }
        }
    ]
    
    for config in test_configs:
        score = predictor.predict(config['features'])
        print(f"   {config['name']:25s}: {score:7.1f} points")
    
    # 5. Сохранение модели
    print("\n5. Saving model...")
    predictor.save('performance_predictor.pkl')
    
    # 6. Детекция аномалий
    print("\n6. Training anomaly detector...")
    detector = AnomalyDetector(contamination=0.1)
    detector.train(df.values)
    
    # Генерация тестовых данных с аномалиями
    test_data = df.sample(100).values
    predictions = detector.detect(test_data)
    n_anomalies = (predictions == -1).sum()
    print(f"   Detected {n_anomalies} anomalies out of 100 samples")
    
    # 7. Roofline анализ
    print("\n7. Roofline model analysis...")
    peak_gflops = 1000  # 1 TFLOPS
    bandwidth_gbs = 50   # 50 GB/s (DDR4-3200 dual-channel)
    
    intensities = [0.1, 0.5, 1.0, 5.0, 10.0, 20.0]
    print("\n   Operational Intensity (FLOP/Byte) → Performance (GFLOPS)")
    for intensity in intensities:
        perf = roofline_model(peak_gflops, bandwidth_gbs, intensity)
        bottleneck = "Memory-bound" if perf < peak_gflops else "Compute-bound"
        print(f"   {intensity:5.1f} → {perf:7.1f} GFLOPS ({bottleneck})")
    
    print("\n═══════════════════════════════════════════════════════")
    print("ML prediction complete!")
    print("═══════════════════════════════════════════════════════")
