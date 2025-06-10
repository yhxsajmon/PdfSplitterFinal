using System;
using System.Collections;
using System.IO;
using System.Linq;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf.Canvas.Parser.Listener;


// Console.WriteLine($"Estratto: {estratto}");
namespace PdfSplitterAndRenaimer
{
    public class PdfSplitter
    {
        static void Main(string[] args)
        {

            List<String> pdf = new List<String>();
            var inputPath = new DirectoryInfo(args[0]);
            String backupAddress = args[1];
            String outputAddress = args[2];

            try
            {
                /*TODO:
                AGGIUNGERE SUPPORTO PER PIU' FILE :PDF  DONE
                Far terminare l'eseguibile DONE
                Modificare i path di input e output
                AGGIUNGERE SUPPORTO PER LA SECONDA VERSIONE DEL PDF
                Spostamento del file input
                */


                //Esegue l'operazione per tutti i pdf
                foreach (FileInfo file in inputPath.GetFiles("*.pdf"))
                {
                    using (PdfDocument pdfDoc = new PdfDocument(new PdfReader(file)))
                    {
                        int totalPages = pdfDoc.GetNumberOfPages();

                        for (int i = 1; i <= totalPages; i++)
                        {
                            string tempFileName = $"pagina_{i}.pdf";
                            string outputPath = Path.Combine(outputAddress, tempFileName);

                            // Splitta la pagina
                            using (PdfDocument newDoc = new PdfDocument(new PdfWriter(outputPath)))
                            {
                                pdfDoc.CopyPagesTo(i, i, newDoc);
                            }

                            // Ora apri il PDF appena creato ed estrai il testo
                            string estratto = EstraiTestoDaPagina(outputPath);

                            // Estrai il nome operatore e il mese/anno
                            var (nomeOperatore, meseAnno) = TrovaOperatoreEMeseAnno(estratto);

                            if (!string.IsNullOrWhiteSpace(nomeOperatore) && !string.IsNullOrWhiteSpace(meseAnno))
                            {


                                // Pulisci il nome
                                string[] nomeParti = nomeOperatore.Split(' ');
                                string cognome = nomeParti.Length > 0 ? nomeParti[0] : "";

                                string nome = string.Join(" ", nomeParti.Skip(1));


                                nome = nome.Replace(" ", "_");
                                Console.WriteLine($"Nome: {nome}");
                                string nuovoNome = Path.Combine(outputAddress, $"{meseAnno}_Timesheet_{cognome}_{nome}.pdf");

                                // Evita errori se il file esiste già
                                if (!File.Exists(nuovoNome))
                                {
                                    File.Move(outputPath, nuovoNome);
                                    Console.WriteLine($"Pagina {i} -> {nomeOperatore} ({meseAnno})");
                                }
                                else
                                {
                                    File.Delete(outputPath);
                                    Console.WriteLine($"Pagina {i} -> file già esistente. Saltata.");
                                }
                            }
                            else
                            {
                                Console.WriteLine($"Pagina {i} -> nome operatore o mese/anno non trovato.");
                            }
                        }

                        Console.WriteLine($"Splittate {totalPages} pagine in {outputAddress}");
                    }
                    try
                    {
                        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                        string nomeFileOriginale = Path.GetFileNameWithoutExtension(file.Name);
                        string estensione = Path.GetExtension(file.Name);
        
                        string nomeBackup = $"{nomeFileOriginale}_processato_{timestamp}{estensione}";
                        string pathBackup = Path.Combine(backupAddress, nomeBackup);

                        // Assicurati che la directory di backup esista
                        Directory.CreateDirectory(backupAddress);

                        File.Move(file.FullName, pathBackup);
                        Console.WriteLine($"File originale spostato in backup: {nomeBackup}");
                    }   
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Errore spostando il file nel backup: {ex.Message}");
                    }
                }   
                
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Errore: {ex.Message}");
            }

        }

                


                

        static string EstraiTestoDaPagina(string filePath)
        {
            using (var pdfDoc = new PdfDocument(new PdfReader(filePath)))
            {
                var strategy = new SimpleTextExtractionStrategy();
                return PdfTextExtractor.GetTextFromPage(pdfDoc.GetPage(1), strategy);
            }
        }

        

        static (string? nomeOperatore, string? meseAnno) TrovaOperatoreEMeseAnno(string testo)
{
    string? nomeOperatore = null;
    string? meseAnno = null;

    foreach (var line in testo.Split('\n'))
    {
        var lineToLower = line.ToLower().Trim();
        
        if (lineToLower.Contains("operatore"))
        {
            Console.WriteLine($"Linea trovata: {line}");
            string[] parole = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            
            Console.WriteLine($"Parole estratte ({parole.Length}): [{string.Join(", ", parole)}]");

            // Formato 2: inizia con "Periodo:"
            if (lineToLower.Contains("periodo:"))
            {
                Console.WriteLine("Rilevato Formato 2 (inizia con 'Periodo:')");
                var result = ParseFormato2(parole);
                if (result.nomeOperatore != null)
                {
                    nomeOperatore = result.nomeOperatore;
                    meseAnno = result.meseAnno;
                }
            }
            // Formato 1: inizia con "Operatore"
            else
            {
                Console.WriteLine("Rilevato Formato 1 (inizia con 'Operatore')");
                var result = ParseFormato1(parole);
                if (result.nomeOperatore != null)
                {
                    nomeOperatore = result.nomeOperatore;
                    meseAnno = result.meseAnno;
                }
            }
        }
    }

    return (nomeOperatore?.Trim(), meseAnno?.Trim());
}

// Formato 1: Operatore [Nomi] Codice Fiscale [CF] Periodo: [Mese] [Anno]// Formato 1: Operatore [Nomi] Codice Fiscale [CF] Periodo: [Mese] [Anno]
static (string? nomeOperatore, string? meseAnno) ParseFormato1(string[] parole)
{
    Console.WriteLine("=== DEBUG ParseFormato1 ===");
    for (int i = 0; i < parole.Length; i++)
    {
        Console.WriteLine($"  [{i}]: '{parole[i]}'");
    }

    if (parole.Length < 8)
    {
        Console.WriteLine($"Formato 1: Troppo poche parole ({parole.Length} < 8)");
        return (null, null);
    }

    // Trova la posizione di "Periodo:" per sapere dove finiscono i nomi
    int indicePeriodo = -1;
    for (int i = 0; i < parole.Length; i++)
    {
        if (parole[i].ToLower().Contains("periodo"))
        {
            indicePeriodo = i;
            break;
        }
    }

    if (indicePeriodo == -1)
    {
        Console.WriteLine("Formato 1: 'Periodo:' non trovato");
        return (null, null);
    }

    Console.WriteLine($"Periodo trovato alla posizione: {indicePeriodo}");

    // MESE E ANNO: dopo "Periodo:"
    string mese = parole.ElementAtOrDefault(indicePeriodo + 1) ?? "";
    string anno = parole.ElementAtOrDefault(indicePeriodo + 2) ?? "";
    
    Console.WriteLine($"Mese (pos {4}): '{mese}'");
    Console.WriteLine($"Anno (pos {5}): '{anno}'");

    string? meseAnno = null;
    if (!string.IsNullOrWhiteSpace(mese) && !string.IsNullOrWhiteSpace(anno) && int.TryParse(anno, out _))
    {
        string meseNumerico = GetMeseNumerico(mese);
        meseAnno = $"{anno}{meseNumerico}";
        Console.WriteLine($"MeseAnno risultante: '{meseAnno}'");
    }
    else
    {
        Console.WriteLine($"ERRORE: Mese o Anno non validi. Mese='{mese}', Anno='{anno}'");
    }

    // Trova "Codice" per sapere dove finiscono i nomi
    int indiceCodeice = -1;
    for (int i = 0; i < indicePeriodo; i++) // Cerca "Codice" prima di "Periodo:"
    {
        if (parole[i].ToLower() == "codice")
        {
            indiceCodeice = i;
            break;
        }
    }

    if (indiceCodeice == -1)
    {
        Console.WriteLine("Formato 1: 'Codice' non trovato");
        return (null, null);
    }

    Console.WriteLine($"Codice trovato alla posizione: {indiceCodeice}");

    // NOMI: dalla posizione 1 (dopo "Operatore") fino a "Codice" (escluso)
    var nomiParti = new List<string>();
    
    for (int i = 6; i < parole.Length -1 ; i++) // Da 1 fino a indiceCodeice (escluso)
    {
        nomiParti.Add(parole[i]);
        Console.WriteLine($"Aggiunto nome da pos [{i}]: '{parole[i]}'");
    }

    if (nomiParti.Count > 0)
    {
        string nomeOperatore = string.Join(" ", nomiParti).Trim();
        Console.WriteLine($"Nome operatore finale: '{nomeOperatore}'");
        return (nomeOperatore, meseAnno);
    }
    else
    {
        Console.WriteLine("ERRORE: Nessun nome trovato");
        return (null, meseAnno);
    }
}



// Formato 2: Operatore [Nome Cognome] [CodFiscale] [CodFiscale] periodo : [Mese Anno]
static (string? nomeOperatore, string? meseAnno) ParseFormato2(string[] parole)
{
    const int PAROLE_MINIME = 9;
    
    if (parole.Length < PAROLE_MINIME)
    {
        Console.WriteLine($"Formato 2: Troppo poche parole ({parole.Length} < {PAROLE_MINIME})");
        return (null, null);
    }

    // Calcola quante parole in eccesso ci sono (nomi aggiuntivi)
    int paroleInEccesso = parole.Length - PAROLE_MINIME;
    Console.WriteLine($"Parole totali: {parole.Length}, Parole in eccesso: {paroleInEccesso}");

    // Estrai nome operatore con logica dinamica
    string nomeOperatore = ExtractNomeOperatoreFormato2(parole, paroleInEccesso);

    // Estrai mese e anno (sempre nelle ultime due posizioni)
    string mese = parole[parole.Length - 2]; // Penultima parola
    string anno = parole[parole.Length - 1]; // Ultima parola

    string? meseAnno = null;
    if (!string.IsNullOrWhiteSpace(mese) && !string.IsNullOrWhiteSpace(anno) && int.TryParse(anno, out _))
    {
        string meseNumerico = GetMeseNumerico(mese);
        meseAnno = $"{anno}{meseNumerico}";
    }

    Console.WriteLine($"Formato 2 - Nome operatore: {nomeOperatore}");
    Console.WriteLine($"Formato 2 - Mese/Anno: {meseAnno}");

    return (nomeOperatore, meseAnno);
}

static string ExtractNomeOperatoreFormato2(string[] parole, int paroleInEccesso)
{
    var nomiParti = new List<string>();
    
    // Sempre prendi posizione 1 (cognome) e 2 (primo nome)
    nomiParti.Add(parole[1]); // Cognome
    nomiParti.Add(parole[2]); // Primo nome
    
    // Aggiungi nomi aggiuntivi basandoti sulle parole in eccesso
    if (paroleInEccesso > 0)
    {
        nomiParti.Add(parole[3]); // Secondo nome
        Console.WriteLine("Aggiunto secondo nome dalla posizione 3");
    }
    
    if (paroleInEccesso > 1)
    {
        nomiParti.Add(parole[4]); // Terzo nome
        Console.WriteLine("Aggiunto terzo nome dalla posizione 4");
    }
    
    // Se servono più nomi (caso raro), continua la logica
    for (int i = 2; i < paroleInEccesso; i++)
    {
        int posizione = 3 + i;
        if (posizione < parole.Length - 6) // Lascia spazio per "Codice Fiscale CF Periodo: Mese Anno"
        {
            nomiParti.Add(parole[posizione]);
            Console.WriteLine($"Aggiunto nome aggiuntivo dalla posizione {posizione}");
        }
    }
    
    return string.Join(" ", nomiParti).Trim();
}
        
    static string GetMeseNumerico(string mese)
        {
            switch (mese.ToUpper())
            {
                case "GENNAIO": return "01";
                case "FEBBRAIO": return "02";
                case "MARZO": return "03";
                case "APRILE": return "04";
                case "MAGGIO": return "05";
                case "GIUGNO": return "06";
                case "LUGLIO": return "07";
                case "AGOSTO": return "08";
                case "SETTEMBRE": return "09";
                case "OTTOBRE": return "10";
                case "NOVEMBRE": return "11";
                case "DICEMBRE": return "12";
                default: return "";
            }
        }
    }
}