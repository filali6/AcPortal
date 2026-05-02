import { Injectable } from '@angular/core';

@Injectable({ providedIn: 'root' })
export class UtilsService {

  getTaskStatusLabel(status: number): string {
    switch (status) {
      case 0: return 'Pending';
      case 1: return 'Blocked';
      case 2: return 'Done';
      default: return '—';
    }
  }

  getTaskStatusColor(status: number): string {
    switch (status) {
      case 0: return '#f59e0b';
      case 1: return '#ef4444';
      case 2: return '#10b981';
      default: return '#888';
    }
  }

  getContractStatusLabel(status: number): string {
    switch (status) {
      case 0: return 'Signed';
      case 1: return 'Project Created';
      case 2: return 'In Progress';
      default: return '—';
    }
  }

  getContractStatusColor(status: number): string {
    switch (status) {
      case 0: return '#f59e0b';
      case 1: return '#10b981';
      case 2: return '#6366f1';
      default: return '#888';
    }
  }

  isContractDelayed(contract: any): boolean {
    if (contract.projectId) return false;
    const days = (new Date().getTime() - new Date(contract.createdAt).getTime()) / (1000 * 60 * 60 * 24);
    return days > 7;
  }

  formatDate(date: string): string {
    return new Date(date).toLocaleDateString('en-GB');
  }
}