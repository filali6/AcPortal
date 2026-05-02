import { Injectable } from '@angular/core';
import { Chart } from 'chart.js';

@Injectable({ providedIn: 'root' })
export class ChartService {

  createDoughnut(
    canvasId: string,
    labels: string[],
    data: number[],
    colors: string[],
    existingChart?: Chart | null
  ): Chart | null {
    const canvas = document.getElementById(canvasId) as HTMLCanvasElement;
    if (!canvas) return null;
    if (existingChart) existingChart.destroy();

    return new Chart(canvas, {
      type: 'doughnut',
      data: {
        labels,
        datasets: [{
          data,
          backgroundColor: colors,
          borderWidth: 0
        }]
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        plugins: {
          legend: {
            position: 'bottom',
            labels: { font: { size: 11 } }
          }
        },
        cutout: '70%'
      }
    });
  }

  createBar(
    canvasId: string,
    labels: string[],
    data: number[],
    color: string = '#3b82f6',
    existingChart?: Chart | null
  ): Chart | null {
    const canvas = document.getElementById(canvasId) as HTMLCanvasElement;
    if (!canvas) return null;
    if (existingChart) existingChart.destroy();

    return new Chart(canvas, {
      type: 'bar',
      data: {
        labels,
        datasets: [{
          data,
          backgroundColor: color,
          borderRadius: 4,
          borderSkipped: false
        }]
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        plugins: { legend: { display: false } },
        scales: {
          y: {
            beginAtZero: true,
            ticks: { stepSize: 1, font: { size: 11 } },
            grid: { color: '#f1f5f9' }
          },
          x: {
            ticks: { font: { size: 11 } },
            grid: { display: false }
          }
        }
      }
    });
  }

  getLast6MonthsLabels(): string[] {
    const months = [];
    const now = new Date();
    for (let i = 5; i >= 0; i--) {
      const d = new Date(now.getFullYear(), now.getMonth() - i, 1);
      months.push(d.toLocaleDateString('en-US', { month: 'short', year: '2-digit' }));
    }
    return months;
  }

  getLast6MonthsData(items: any[], dateField: string = 'createdAt'): number[] {
    const now = new Date();
    const result = [];
    for (let i = 5; i >= 0; i--) {
      const d = new Date(now.getFullYear(), now.getMonth() - i, 1);
      result.push(items.filter(item => {
        const cd = new Date(item[dateField]);
        return cd.getMonth() === d.getMonth() && cd.getFullYear() === d.getFullYear();
      }).length);
    }
    return result;
  }
}