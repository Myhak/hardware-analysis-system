"""
Симулятор кэш-памяти с различными политиками замещения
"""

from enum import Enum
from typing import List, Dict, Optional, Tuple
from collections import OrderedDict, deque
import matplotlib.pyplot as plt
import seaborn as sns


class ReplacementPolicy(Enum):
    """Политики замещения кэша"""
    LRU = "Least Recently Used"
    FIFO = "First In First Out"
    LFU = "Least Frequently Used"
    RANDOM = "Random"


class CacheSimulator:
    """Симулятор кэш-памяти"""
    
    def __init__(self, capacity: int, policy: ReplacementPolicy = ReplacementPolicy.LRU):
        """
        Args:
            capacity: Количество блоков в кэше
            policy: Политика замещения
        """
        self.capacity = capacity
        self.policy = policy
        self.cache: OrderedDict = OrderedDict()
        self.frequency: Dict[int, int] = {}
        self.fifo_queue: deque = deque()
        
        # Статистика
        self.hits = 0
        self.misses = 0
        self.accesses = []
        self.hit_rate_history = []
        
    def access(self, address: int) -> bool:
        """
        Доступ к адресу памяти
        
        Args:
            address: Адрес блока памяти
            
        Returns:
            True если попадание (hit), False если промах (miss)
        """
        self.accesses.append(address)
        
        if address in self.cache:
            # Попадание в кэш
            self.hits += 1
            self._update_on_hit(address)
            self.hit_rate_history.append(self.get_hit_rate())
            return True
        else:
            # Промах кэша
            self.misses += 1
            self._update_on_miss(address)
            self.hit_rate_history.append(self.get_hit_rate())
            return False
    
    def _update_on_hit(self, address: int):
        """Обновление при попадании"""
        if self.policy == ReplacementPolicy.LRU:
            # Перемещаем в конец (самый свежий)
            self.cache.move_to_end(address)
        elif self.policy == ReplacementPolicy.LFU:
            # Увеличиваем счётчик
            self.frequency[address] = self.frequency.get(address, 0) + 1
    
    def _update_on_miss(self, address: int):
        """Обновление при промахе"""
        if len(self.cache) >= self.capacity:
            # Кэш заполнен, нужно вытеснить блок
            self._evict()
        
        # Добавляем новый блок
        self.cache[address] = True
        
        if self.policy == ReplacementPolicy.LFU:
            self.frequency[address] = 1
        elif self.policy == ReplacementPolicy.FIFO:
            self.fifo_queue.append(address)
        elif self.policy == ReplacementPolicy.LRU:
            self.cache.move_to_end(address)
    
    def _evict(self):
        """Вытеснение блока из кэша"""
        if self.policy == ReplacementPolicy.LRU:
            # Удаляем самый старый (первый)
            evicted_addr = next(iter(self.cache))
            del self.cache[evicted_addr]
            
        elif self.policy == ReplacementPolicy.FIFO:
            # Удаляем первый добавленный
            evicted_addr = self.fifo_queue.popleft()
            del self.cache[evicted_addr]
            
        elif self.policy == ReplacementPolicy.LFU:
            # Удаляем наименее часто используемый
            min_freq_addr = min(self.frequency, key=self.frequency.get)
            del self.cache[min_freq_addr]
            del self.frequency[min_freq_addr]
            
        elif self.policy == ReplacementPolicy.RANDOM:
            # Случайное вытеснение
            import random
            evicted_addr = random.choice(list(self.cache.keys()))
            del self.cache[evicted_addr]
    
    def get_hit_rate(self) -> float:
        """Вычисление коэффициента попаданий"""
        total = self.hits + self.misses
        return (self.hits / total * 100) if total > 0 else 0.0
    
    def get_statistics(self) -> Dict:
        """Получение статистики"""
        return {
            'hits': self.hits,
            'misses': self.misses,
            'total_accesses': self.hits + self.misses,
            'hit_rate': self.get_hit_rate(),
            'miss_rate': 100 - self.get_hit_rate()
        }
    
    def visualize_performance(self, save_path: Optional[str] = None):
        """Визуализация производительности кэша"""
        fig, axes = plt.subplots(2, 2, figsize=(14, 10))
        
        # 1. Hit rate с течением времени
        axes[0, 0].plot(self.hit_rate_history, linewidth=1)
        axes[0, 0].set_title('Cache Hit Rate Over Time')
        axes[0, 0].set_xlabel('Access Number')
        axes[0, 0].set_ylabel('Hit Rate (%)')
        axes[0, 0].grid(True, alpha=0.3)
        
        # 2. Pie chart: hits vs misses
        stats = self.get_statistics()
        axes[0, 1].pie([stats['hits'], stats['misses']], 
                       labels=['Hits', 'Misses'],
                       autopct='%1.1f%%',
                       colors=['#2ecc71', '#e74c3c'])
        axes[0, 1].set_title('Hit vs Miss Distribution')
        
        # 3. Гистограмма частоты доступа к адресам
        from collections import Counter
        address_counts = Counter(self.accesses)
        top_addresses = sorted(address_counts.items(), 
                               key=lambda x: x[1], 
                               reverse=True)[:20]
        
        if top_addresses:
            addrs, counts = zip(*top_addresses)
            axes[1, 0].bar(range(len(addrs)), counts)
            axes[1, 0].set_title('Top 20 Most Accessed Addresses')
            axes[1, 0].set_xlabel('Address Rank')
            axes[1, 0].set_ylabel('Access Count')
            axes[1, 0].grid(True, alpha=0.3, axis='y')
        
        # 4. Summary statistics
        axes[1, 1].axis('off')
        summary_text = f"""
        Cache Simulation Summary
        ═══════════════════════════
        
        Policy: {self.policy.value}
        Capacity: {self.capacity} blocks
        
        Total Accesses: {stats['total_accesses']:,}
        Hits: {stats['hits']:,}
        Misses: {stats['misses']:,}
        
        Hit Rate: {stats['hit_rate']:.2f}%
        Miss Rate: {stats['miss_rate']:.2f}%
        """
        axes[1, 1].text(0.1, 0.5, summary_text, 
                        fontsize=12, 
                        family='monospace',
                        verticalalignment='center')
        
        plt.tight_layout()
        
        if save_path:
            plt.savefig(save_path, dpi=150)
            print(f"Chart saved to: {save_path}")
        
        plt.show()


def compare_policies(access_pattern: List[int], cache_sizes: List[int]):
    """Сравнение различных политик замещения"""
    
    policies = [ReplacementPolicy.LRU, ReplacementPolicy.FIFO, 
                ReplacementPolicy.LFU, ReplacementPolicy.RANDOM]
    
    results = {policy.value: [] for policy in policies}
    
    for size in cache_sizes:
        print(f"\nTesting cache size: {size}")
        
        for policy in policies:
            sim = CacheSimulator(size, policy)
            
            for addr in access_pattern:
                sim.access(addr)
            
            hit_rate = sim.get_hit_rate()
            results[policy.value].append(hit_rate)
            print(f"  {policy.value}: {hit_rate:.2f}% hit rate")
    
    # Визуализация сравнения
    plt.figure(figsize=(12, 6))
    
    for policy_name, hit_rates in results.items():
        plt.plot(cache_sizes, hit_rates, marker='o', label=policy_name, linewidth=2)
    
    plt.xlabel('Cache Size (blocks)')
    plt.ylabel('Hit Rate (%)')
    plt.title('Cache Performance: Policy Comparison')
    plt.legend()
    plt.grid(True, alpha=0.3)
    plt.savefig('cache_policy_comparison.png', dpi=150)
    plt.show()


def generate_access_pattern(pattern_type: str, length: int) -> List[int]:
    """Генерация паттерна доступа к памяти"""
    import random
    import numpy as np
    
    if pattern_type == "sequential":
        # Последовательный доступ: 0, 1, 2, 3, ...
        return list(range(length))
    
    elif pattern_type == "random":
        # Случайный доступ
        return [random.randint(0, length // 2) for _ in range(length)]
    
    elif pattern_type == "locality":
        # Доступ с пространственной локальностью
        pattern = []
        current = 0
        for _ in range(length):
            pattern.append(current)
            # 80% вероятность остаться рядом, 20% - прыжок
            if random.random() < 0.8:
                current += random.randint(1, 5)
            else:
                current += random.randint(20, 100)
        return pattern
    
    elif pattern_type == "stride":
        # Stride access (например, каждый 4-й элемент)
        stride = 4
        return [i * stride for i in range(length)]
    
    return []


if __name__ == "__main__":
    print("═══════════════════════════════════════════════════════")
    print("           Cache Simulator (Stage 5)")
    print("═══════════════════════════════════════════════════════\n")
    
    # 1. Тест с локальностью доступа
    print("1. Testing with locality pattern...")
    pattern = generate_access_pattern("locality", 10000)
    
    sim = CacheSimulator(capacity=64, policy=ReplacementPolicy.LRU)
    
    for addr in pattern:
        sim.access(addr)
    
    stats = sim.get_statistics()
    print(f"   Hit Rate: {stats['hit_rate']:.2f}%")
    print(f"   Misses: {stats['misses']:,}\n")
    
    sim.visualize_performance("cache_lru_performance.png")
    
    # 2. Сравнение политик
    print("\n2. Comparing replacement policies...")
    cache_sizes = [16, 32, 64, 128, 256]
    compare_policies(pattern, cache_sizes)
    
    # 3. Различные паттерны доступа
    print("\n3. Testing different access patterns...")
    patterns = {
        "Sequential": generate_access_pattern("sequential", 1000),
        "Random": generate_access_pattern("random", 1000),
        "Locality": generate_access_pattern("locality", 1000),
        "Stride": generate_access_pattern("stride", 1000)
    }
    
    for pattern_name, pattern_data in patterns.items():
        sim = CacheSimulator(capacity=64, policy=ReplacementPolicy.LRU)
        for addr in pattern_data:
            sim.access(addr)
        
        print(f"   {pattern_name}: {sim.get_hit_rate():.2f}% hit rate")
    
    print("\n═══════════════════════════════════════════════════════")
    print("Simulation complete!")
    print("═══════════════════════════════════════════════════════")
