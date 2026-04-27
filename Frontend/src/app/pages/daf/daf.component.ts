import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { AuthService } from '../../core/services/auth.service';
import { ContractsService } from '../../core/services/contracts.service';
import { TabsService } from '../../core/services/tabs.service';

@Component({
  selector: 'app-daf',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './daf.component.html',
  styleUrl: './daf.component.scss'
})
export class DafComponent implements OnInit {

  activeTabId = 'tasks';
  stats = { total: 0, projectCreated: 0, pending: 0 };
  contracts: any[] = [];
  loading = false;
  showNewContractForm = false;
contractCreated = false;

  openTabs: { [tabId: string]: any } = {};

  // Formulaire nouveau contrat
  newContract = {
    clientName: '',
    description: '',
    files: [] as File[]
  };

  // Formulaire ajout fichiers
  addFilesData: { [contractId: string]: File[] } = {};

  successMessage = '';
  errorMessage = '';

  constructor(
    private contractsService: ContractsService,
    private authService: AuthService,
    private tabsService: TabsService
  ) {}

  ngOnInit(): void {
    this.loadData();
    this.tabsService.activeTabId.subscribe(id => this.activeTabId = id);
  }

  loadData(): void {
    this.loading = true;
    this.contractsService.getMyContracts().subscribe({
      next: (data) => {
        this.stats = data.stats;
        this.contracts = data.contracts;
        this.loading = false;
      },
      error: () => this.loading = false
    });
  }

  openNewContractTab(): void {
  this.tabsService.setActiveTab('tasks');
  this.showNewContractForm = true;
}

  openContractTab(contract: any): void {
    const tabId = `contract-${contract.id}`;
    if (!this.openTabs[tabId]) {
      this.openTabs[tabId] = { contract, addFiles: [] };
    }
    this.tabsService.openTab({
      id: tabId,
      title: contract.clientName,
      type: 'create-project'
    });
  }

  getOpenTabIds(): string[] {
    return Object.keys(this.openTabs);
  }

  getTabData(tabId: string): any {
    return this.openTabs[tabId] || null;
  }

  onFilesSelected(event: any): void {
    this.newContract.files = Array.from(event.target.files);
  }

  onAddFilesSelected(event: any, contractId: string): void {
    this.addFilesData[contractId] = Array.from(event.target.files);
  }

  createContract(): void {
  if (!this.newContract.clientName) {
    this.errorMessage = 'Nom du client obligatoire';
    return;
  }
  this.loading = true;
  this.errorMessage = '';

  const formData = new FormData();
  formData.append('clientName', this.newContract.clientName);
  formData.append('description', this.newContract.description);
  this.newContract.files.forEach(f => formData.append('files', f));

  this.contractsService.create(formData).subscribe({
    next: () => {
      this.successMessage = 'Contrat signé — HeadOfCDS notifié !';
      this.loading = false;
      this.contractCreated = true;
      this.loadData();
    },
    error: () => {
      this.errorMessage = 'Erreur lors de la signature';
      this.loading = false;
    }
  });
}

  addFiles(contractId: string): void {
    const files = this.addFilesData[contractId];
    if (!files?.length) return;

    const formData = new FormData();
    files.forEach(f => formData.append('files', f));

    this.contractsService.addFiles(contractId, formData).subscribe({
      next: () => {
        this.successMessage = 'Fichiers ajoutés !';
        this.loadData();
        delete this.addFilesData[contractId];
      }
    });
  }

  getFileUrl(fileName: string): string {
    return this.contractsService.getFileUrl(fileName);
  }

  getStatusLabel(status: number): string {
    switch (status) {
      case 0: return 'Signé';
      case 1: return 'Projet créé';
      case 2: return 'En cours';
      default: return '—';
    }
  }

  getStatusColor(status: number): string {
    switch (status) {
      case 0: return '#f59e0b';
      case 1: return '#10b981';
      case 2: return '#6366f1';
      default: return '#888';
    }
  }
  endNewContract(): void {
  this.showNewContractForm = false;
  this.contractCreated = false;
  this.newContract = { clientName: '', description: '', files: [] };
  this.successMessage = '';
}
}