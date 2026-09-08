import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ContractsService } from '../../core/services/contracts.service';
import { NotificationService } from '../../core/services/notification.service';
import { TabsService } from '../../core/services/tabs.service';
import { ToastService } from '../../core/services/toast.service';
import { Subscription } from 'rxjs';
import { Chart, registerables } from 'chart.js';
import { UtilsService } from '../../core/services/utils.service';
import { ModalComponent } from '../../core/components/modal/modal.component';
Chart.register(...registerables);
import { ChartService } from '../../core/services/chart.service';
import { TranslateModule,TranslateService } from '@ngx-translate/core';

@Component({
  selector: 'app-daf',
  standalone: true,
  imports: [CommonModule, FormsModule,ModalComponent,TranslateModule],
  templateUrl: './daf.component.html',
  styleUrl: './daf.component.scss'
})
export class DafComponent implements OnInit, OnDestroy {

  activeTabId: string = 'tasks';
  tabs: { id: string; title: string }[] = [];
  contracts: any[] = [];
  loading = false;

  // Modal nouveau contrat
  showNewContractModal = false;
  newContract = { clientName: '', description: '', files: [] as File[] };
  newContractLoading = false;

  // Données par onglet
  openTabs: { [tabId: string]: any } = {};

 statsBottom = {
  total: 0,
  converted: 0,
  conversionRate: 0,
  avgDays: 0,
  waitingOver7Days: 0
};
  

  private donutChart: Chart | null = null;
  private barChart: Chart | null = null;
  private subs: Subscription[] = [];

  constructor(
    private contractsService: ContractsService,
    private notificationService: NotificationService,
    private tabsService: TabsService,
    private toastService: ToastService,
    public utils : UtilsService,
    private chartService:ChartService,
    private translate:TranslateService
  ) {}

  ngOnInit(): void {
    this.loadData();
    this.subs.push(
      this.tabsService.activeTabId.subscribe(id => this.activeTabId = id),
      this.tabsService.tabs.subscribe(tabs => this.tabs = tabs),
      this.notificationService.notifications$.subscribe(() => this.loadData())
    );
  }

  ngOnDestroy(): void {
    this.subs.forEach(s => s.unsubscribe());
  }

  loadData(): void {
    this.loading = true;
    this.contractsService.getMyContracts().subscribe({
      next: (data) => {
        this.contracts = data.contracts;
        this.computeBottomStats();

        Object.keys(this.openTabs).forEach(tabId => {
          const contractId = this.openTabs[tabId]?.contract?.id;
          if (contractId) {
            const updated = this.contracts.find(c => c.id === contractId);
            if (updated) this.openTabs[tabId].contract = { ...updated };
          }
        });

        this.loading = false;
      },
      error: () => this.loading = false
    });
  }

  

   

  closeNewContractModal(): void {
    this.showNewContractModal = false;
    this.newContract = { clientName: '', description: '', files: [] };
  }

  onNewFilesSelected(event: any): void {
    const newFiles = Array.from(event.target.files) as File[];
    this.newContract.files = [...this.newContract.files, ...newFiles];
  }

  createContract(): void {
    if (!this.newContract.clientName.trim()) {
      this.toastService.show('Client name is required', 'error');
      return;
    }
    if (!this.newContract.description.trim()) {
      this.toastService.show('Description is required', 'error');
      return;
    }
    if (this.newContract.files.length === 0) {
      this.toastService.show('At least one file is required', 'error');
      return;
    }

    this.newContractLoading = true;
    const formData = new FormData();
    formData.append('clientName', this.newContract.clientName);
    formData.append('description', this.newContract.description);
    this.newContract.files.forEach(f => formData.append('files', f));

    this.contractsService.create(formData).subscribe({
      next: () => {
        this.toastService.show('Contract signed — HeadOfCDS notified!', 'success');
        this.newContractLoading = false;
        this.closeNewContractModal();
        this.loadData();
      },
      error: () => {
        this.toastService.show('Error signing contract', 'error');
        this.newContractLoading = false;
      }
    });
  }

   

  setActiveTab(tabId: string): void {
    this.tabsService.setActiveTab(tabId);
  }

  openContractTab(contract: any): void {
    const tabId = `contract-${contract.id}`;
    if (!this.openTabs[tabId]) {
      this.openTabs[tabId] = {
        contract: { ...contract },
        editMode: true, // ouvert directement en mode édition
        editData: {
          clientName: contract.clientName,
          description: contract.description,
          status: contract.status,
          newFiles: [] as File[]
        }
      };
    }
    this.tabsService.openTab({ id: tabId, title: contract.clientName, type: 'create-project' });
  }

  closeTab(tabId: string, event: MouseEvent): void {
    event.stopPropagation();
    delete this.openTabs[tabId];
    this.tabsService.closeTab(tabId);
  }

  getOpenTabIds(): string[] {
    return Object.keys(this.openTabs);
  }

  getTabData(tabId: string): any {
    return this.openTabs[tabId] || null;
  }

  cancelEdit(tabId: string): void {
    const tab = this.openTabs[tabId];
    if (!tab) return;
    tab.editMode = false;
    tab.editData = {
      clientName: tab.contract.clientName,
      description: tab.contract.description,
      status: tab.contract.status,
      newFiles: []
    };
  }

  onEditFilesSelected(event: any, tabId: string): void {
    const newFiles = Array.from(event.target.files) as File[];
    this.openTabs[tabId].editData.newFiles = [
      ...this.openTabs[tabId].editData.newFiles,
      ...newFiles
    ];
  }

  saveContract(tabId: string): void {
    const tab = this.openTabs[tabId];
    if (!tab) return;

    if (!tab.editData.description.trim()) {
      this.toastService.show('Description is required', 'error');
      return;
    }

    const formData = new FormData();
    formData.append('clientName', tab.contract.clientName);
    formData.append('description', tab.editData.description);
    formData.append('status', tab.contract.status.toString());
    tab.editData.newFiles?.forEach((f: File) => formData.append('newFiles', f));

    this.contractsService.update(tab.contract.id, formData).subscribe({
      next: (updated) => {
        this.openTabs[tabId].contract = { ...updated };
        this.openTabs[tabId].editMode = false;
        this.toastService.show('Contract updated!', 'success');
        this.loadData();
      },
      error: () => {
        this.toastService.show('Error updating contract', 'error');
      }
    });
  }

  deleteFile(tabId: string, fileName: string): void {
    const tab = this.openTabs[tabId];
    if (!tab) return;

    this.contractsService.deleteFile(tab.contract.id, encodeURIComponent(fileName)).subscribe({
      next: (updated) => {
        this.openTabs[tabId].contract = { ...updated };
        this.toastService.show('File deleted!', 'success');
        this.loadData();
      },
      error: () => {
        this.toastService.show('Error deleting file', 'error');
      }
    });
  }

  getFileUrl(fileName: string): string {
    return this.contractsService.getFileUrl(fileName);
  }

  getFileName(fullPath: string): string {
    return fullPath.split('_').slice(1).join('_') || fullPath;
  }

   

  computeBottomStats(): void {
  const converted = this.contracts.filter(c => c.projectId);

  this.statsBottom.total = this.contracts.length;
  this.statsBottom.converted = converted.length;
  this.statsBottom.conversionRate = this.contracts.length
    ? Math.round((converted.length / this.contracts.length) * 100)
    : 0;

  this.statsBottom.avgDays = converted.length
    ? Math.round(
        converted.reduce((sum, c) => sum + this.getDays(c.createdAt), 0) / converted.length
      )
    : 0;

  this.statsBottom.waitingOver7Days = this.contracts.filter(c => this.isDelayed(c)).length;

  setTimeout(() => this.renderCharts(), 100);
}

  renderCharts(): void {
  this.donutChart = this.chartService.createDoughnut(
    'donutChart',
    [
      this.translate.instant('CONTRACTS.CONVERTED'),
      this.translate.instant('CONTRACTS.WAITING'),
      this.translate.instant('CONTRACTS.DELAYED')
    ],
    [
      this.contracts.filter(c => c.projectId).length,
      this.contracts.filter(c => !c.projectId && !this.isDelayed(c)).length,
      this.contracts.filter(c => this.isDelayed(c)).length
    ],
    ['#10b981', '#f59e0b', '#ef4444'],
    this.donutChart
  );

  this.barChart = this.chartService.createBar(
    'barChart',
    this.chartService.getLast6MonthsLabels(),
    this.chartService.getLast6MonthsData(this.contracts, 'createdAt'),
    '#3b82f6',
    this.barChart
  );
}
   
  openNewContractModal(): void {
  this.newContract = { clientName: '', description: '', files: [] };
  this.showNewContractModal = true;
}
getDays(createdAt: string): number {
  const diff = new Date().getTime() - new Date(createdAt).getTime();
  return Math.floor(diff / (1000 * 60 * 60 * 24));
}

isDelayed(contract: any): boolean {
  return !contract.projectId && this.getDays(contract.createdAt) > 7;
}
closeContractTab(tabId: string): void {
  delete this.openTabs[tabId];
  this.tabsService.closeTab(tabId);
}
  

 
  
 
}