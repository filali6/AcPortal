import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../../environments/environment';
import { ContractsService } from '../../../core/services/contracts.service';
import { UtilsService } from '../../../core/services/utils.service';
@Component({
  selector: 'app-contracts-tab',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './contracts-tab.component.html',
  styleUrl: './contracts-tab.component.scss'
})
export class ContractsTabComponent implements OnInit {
  contracts: any[] = [];
  selectedContract: any = null;
  loading = false;

  constructor(
    private http: HttpClient,
    private contractsService: ContractsService,
    public utils:UtilsService
  ) {}

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading = true;
    this.http.get<any[]>(`${environment.apiUrl}/contracts/all`).subscribe({
      next: (c) => { this.contracts = c; this.loading = false; },
      error: () => this.loading = false
    });
  }

  selectContract(contract: any): void {
    this.selectedContract = contract;
  }

  getFileUrl(fileName: string): string {
    return this.contractsService.getFileUrl(fileName);
  }

  getFileName(fullPath: string): string {
    return fullPath.split('_').slice(1).join('_') || fullPath;
  }

   
}